using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using NamedPipeLine.Interfaces;
using NamedPipeLine.Models;

namespace NamedPipeLine.Services
{
    public class NamedPipeServer<T> : IIPCServer<T>, IDisposable where T : class, IIPCMessage, new()
    {
        public event EventHandler<T>? MessageReceived;
        
        private readonly string _pipeName;
        private const int MaxRetryAttempts = 3;
        private const int RetryDelay = 1000;
        
        private readonly IMessageSerializer _serializer;
        private readonly CancellationTokenSource _cancellationToken;
        private readonly SemaphoreSlim _createReleaseLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _messageLock = new SemaphoreSlim(1, 1);
        
        private volatile bool _isRunning;
        private volatile bool _isDisposed;
        
        private NamedPipeServerStream? _pipeServerStream;
        private Task? _messageListenerTask;
        private StreamWriter? _writer;
        private StreamReader? _reader;

        public bool IsPipeValid => IsSafePipe();
        public bool IsRunning => _isRunning;
        public bool IsConnected => IsValidPipeState() && _isRunning;

        public NamedPipeServer(string pipeName, IMessageSerializer? serializer = null)
        {
            _pipeName = pipeName;
            _serializer = serializer ?? new JsonMessageSerializer();
            _cancellationToken = new CancellationTokenSource();
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isRunning)
            {
                return;
            }

            try
            {
                await CreateNewPipeAsync();
                
                _messageListenerTask = StartListeningAsync(cancellationToken);
            }
            catch (Exception e)
            {
                _isRunning = false;
                throw;
            }
        }

        public async Task SendMessageAsync(T message)
        {
            for (int attempt = 0; attempt < MaxRetryAttempts; attempt++)
            {
                await _messageLock.WaitAsync();
                try
                {
                    if (!IsValidPipeState())
                    {
                        await CreateNewPipeAsync();
                    }

                    string messageText = _serializer.Serialize(message);
                    await _writer.WriteLineAsync(messageText);
                    await _writer.FlushAsync();
                }
                catch (PipeCommunicationException e) when (e.FailureType == PipeCommunicationFailureType.PipeDisconnected &&
                                                           attempt < MaxRetryAttempts - 1)
                {
                    continue;
                }
                catch (PipeCommunicationException e) when (e.FailureType == PipeCommunicationFailureType.PipeDisconnected)
                {
                    throw new PipeCommunicationException("Pipe is disconnected", PipeCommunicationFailureType.PipeDisconnected, e);
                }
                catch (IOException) when (attempt < MaxRetryAttempts - 1)
                {
                    await Task.Delay(RetryDelay);
                }
                catch (Exception e)
                {
                    throw new PipeCommunicationException("Failed to send message", PipeCommunicationFailureType.SendFailed, e);
                }
                finally
                {
                    _messageLock.Release();
                }
            }
        }

        /// <summary>
        /// 현재 실행 중인 NamedPipeServerStream을 정리하고, 종료합니다.
        /// NamedPipeServer 객체는 정리하지 않습니다.
        /// </summary>
        public async Task StopAsync()
        {
            await _createReleaseLock.WaitAsync();
            try
            {
                _isRunning = false;
                _cancellationToken.Cancel();

                if (_messageListenerTask != null)
                {
                    await _messageListenerTask;
                }

                await CleanupCurrentPipeAsync();
            }
            finally
            {
                _createReleaseLock.Release();
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }
            
            if (disposing)
            {
                _isRunning = false;
                _cancellationToken.Cancel();
                _createReleaseLock.Dispose();
                _messageLock.Dispose();
                CleanupCurrentPipeAsync().Wait();
            }

            _isDisposed = true;
        }
        
        /// <summary>
        /// 새로운 NamedPipeServerStream을 생성합니다.
        /// 이때, 기존의 NamedPipeServerStream이 연결되어 있다면 연결을 끊고
        /// 연결이 확인되면 StreamReader, StreamWriter를 생성합니다.
        /// </summary>
        private async Task CreateNewPipeAsync()
        {
            await _createReleaseLock.WaitAsync();
            try
            {
                if (_pipeServerStream?.IsConnected == true)
                {
                    try
                    {
                        T changeMessage = new T
                        {
                            MessageType = PipeMessageType.PipeChanged
                        };
                        await SendMessageAsync(changeMessage);
                        await Task.Delay(100);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }
                
                await CleanupCurrentPipeAsync();

                _pipeServerStream = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Message,
                    PipeOptions.Asynchronous);

                await _pipeServerStream.WaitForConnectionAsync(_cancellationToken.Token);
                _writer = new StreamWriter(_pipeServerStream) { AutoFlush = true };
                _reader = new StreamReader(_pipeServerStream);
                
                _isRunning = true;
            }
            catch (Exception)
            {
                await CleanupCurrentPipeAsync();
                throw new PipeCommunicationException("Pipe is not connected", PipeCommunicationFailureType.PipeDisconnected);
            }
            finally
            {
                _createReleaseLock.Release();
            }
        }

        /// <summary>
        /// NamedPipeServerStream, StreamReader, StreamWriter를 정리합니다.
        /// </summary>
        private async Task CleanupCurrentPipeAsync()
        {
            if (_writer != null)
            {
                await _writer.DisposeAsync();
                _writer = null;
            }

            if (_reader != null)
            {
                _reader.Dispose();
                _reader = null;
            }
            
            if (_pipeServerStream != null)
            {
                if (_pipeServerStream.IsConnected)
                {
                    _pipeServerStream.Disconnect();
                }
                await _pipeServerStream.DisposeAsync();
                _pipeServerStream = null;
            }
            
            _isRunning = false;
        }

        /// <summary>
        /// 메시지를 수신하는 작업을 시작합니다.
        /// </summary>
        /// <param name="cancellationToken">작업 취소 토큰</param>
        private async Task StartListeningAsync(CancellationToken cancellationToken)
        {
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken.Token, cancellationToken);

            while (_isRunning && !linkedTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    if (!IsValidPipeState())
                    {
                        await Task.Delay(RetryDelay, linkedTokenSource.Token);
                        continue;
                    }

                    string? messageText = await _reader.ReadLineAsync();
                    if (messageText == null)
                    {
                        throw new InvalidOperationException("Received message is null");
                    }
                    
                    var message = _serializer.Deserialize<T>(messageText);
                    await MessageProcess(message);
                }
                catch (OperationCanceledException) when(linkedTokenSource.Token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception)
                {
                    throw new PipeCommunicationException("Failed to receive message", PipeCommunicationFailureType.ReceiveFailed);
                }
            }
        }
        
        /// <summary>
        /// 수신된 메시지를 처리합니다.
        /// 만약, UserMessage가 아닌 SystemMessage라면, 해당 메시지에 대한 처리를 수행합니다.
        /// </summary>
        /// <param name="message"></param>
        private async Task MessageProcess(T? message)
        {
            if (message == null)
            {
                return;
            }
            
            if (message.MessageType == PipeMessageType.UserMessage)
            {
                MessageReceived?.Invoke(this, message);
            }
        }

        private bool IsSafePipe()
        {
            if (_pipeServerStream == null)
            {
                return false;
            }
            
            try
            {
                _pipeServerStream.SafePipeHandle.DangerousGetHandle();
                return true;
            }
            catch (ObjectDisposedException e)
            {
                return false;
            }
            catch (Exception e)
            {
                return false;
            }
        }
        
        /// <summary>
        /// 파이프가 유효한지, StreamReader, StreamWriter가 생성되었는지 확인합니다.
        /// </summary>
        /// <returns></returns>
        private bool IsValidPipeState()
        {
            return _writer != null &&
                   _reader != null &&
                   _pipeServerStream != null &&
                   _pipeServerStream.IsConnected;
        }
    }
}