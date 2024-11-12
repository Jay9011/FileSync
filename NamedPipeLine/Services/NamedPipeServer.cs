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
        private readonly IMessageSerializer _serializer;
        private readonly CancellationTokenSource _cancellationToken;
        private readonly SemaphoreSlim _createReleaseLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _messageLock = new SemaphoreSlim(1, 1);
        
        private volatile bool _isRunning;
        private volatile bool _shouldReconnect;
        private volatile bool _isDisposed;
        
        private NamedPipeServerStream? _pipeServerStream;
        private Task? _messageListenerTask;
        private StreamWriter? _writer;
        private StreamReader? _reader;

        #region Heartbeat

        private readonly Timer _heartbeatTimer;
        private readonly object _heartbeatLock = new object();
        private DateTime _lastHeartbeatTime = DateTime.MinValue;
        private const int HeartbeatInterval = 1000;
        private const int HeartbeatTimeout = 5000;
        private const int ReconnectDelay = 1000;
        private const int MaxRetryAttempts = 3;

        #endregion

        public NamedPipeServer(string pipeName, IMessageSerializer? serializer = null)
        {
            _pipeName = pipeName;
            _serializer = serializer ?? new JsonMessageSerializer();
            _cancellationToken = new CancellationTokenSource();
            
            _heartbeatTimer = new Timer(HeartbeatCheck, null, HeartbeatInterval, HeartbeatInterval);
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            if (_isRunning)
            {
                return;
            }

            try
            {
                await CreateNewPipeAsync();
                _isRunning = true;
                _heartbeatTimer.Change(0, HeartbeatInterval);
                _messageListenerTask = StartListeningAsync(cancellationToken);
            }
            catch (Exception)
            {
                _isRunning = false;
            }
        }

        public async Task SendMessageAsync(T message)
        {
            ThrowIfDisposed();

            for (int attempt = 0; attempt < MaxRetryAttempts; attempt++)
            {
                await _messageLock.WaitAsync();
                try
                {
                    if (!IsValidPipeState())
                    {
                        if (attempt == MaxRetryAttempts - 1)
                        {
                            throw new InvalidOperationException("Pipe is not connected");
                        }
                        
                        _shouldReconnect = true;
                        await Task.Delay(ReconnectDelay);
                    }

                    string messageText = _serializer.Serialize(message);
                    await _writer.WriteLineAsync(messageText);
                    await _writer.FlushAsync();
                    return;
                }
                catch (IOException) when (attempt < MaxRetryAttempts - 1)
                {
                    _shouldReconnect = true;
                    await Task.Delay(ReconnectDelay * (attempt + 1));
                }
                finally
                {
                    _messageLock.Release();
                }
            }
            throw new CommunicationException("Failed to send message");
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
                _heartbeatTimer.Change(Timeout.Infinite, Timeout.Infinite);
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
                _heartbeatTimer.Dispose();
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
                _shouldReconnect = false;

                lock (_heartbeatLock)
                {
                    _lastHeartbeatTime = DateTime.Now;
                }
            }
            catch (Exception)
            {
                await CleanupCurrentPipeAsync();
                throw;
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
                    if(_shouldReconnect)
                    {
                        await CreateNewPipeAsync();
                    }

                    if (!IsValidPipeState())
                    {
                        _shouldReconnect = true;
                        await Task.Delay(1000, linkedTokenSource.Token);
                        continue;
                    }

                    string? messageText = await _reader.ReadLineAsync();
                    if (messageText == null)
                    {
                        _shouldReconnect = true;
                        continue;
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
                    _shouldReconnect = true;
                    await Task.Delay(1000, linkedTokenSource.Token);
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
            else
            {
                await HandleHeartbeatAck(message);
            }
        }

        /// <summary>
        /// Heartbeat를 체크하고, 만약 클라이언트와의 연결이 끊어졌다고 판단되면 재연결을 시도합니다.
        /// 정상적인 경우, Heartbeat 메시지를 전송합니다.
        /// </summary>
        /// <param name="state"></param>
        private async void HeartbeatCheck(object state)
        {
            if (!_isRunning || _shouldReconnect || _isDisposed)
            {
                return;
            }

            try
            {
                DateTime lastHeartbeatTime;
                lock (_heartbeatLock)
                {
                    lastHeartbeatTime = _lastHeartbeatTime;
                }
                
                if (lastHeartbeatTime != DateTime.MinValue &&
                    DateTime.Now - lastHeartbeatTime > TimeSpan.FromMilliseconds(HeartbeatTimeout))
                {
                    _shouldReconnect = true;
                    return;
                }

                T heartbeat = new T
                {
                    MessageType = PipeMessageType.Heartbeat
                };
                
                await SendMessageAsync(heartbeat);
            }
            catch (Exception)
            {
                _shouldReconnect = true;
            }
        }

        /// <summary>
        /// HeartbeatAck 메시지를 처리합니다.
        /// 만약, PipeChanged 요구 메시지라면, 재연결을 시도합니다.
        /// </summary>
        /// <param name="message"></param>
        private async Task HandleHeartbeatAck(T? message)
        {
            if (message == null)
            {
                return;
            }
            
            if (message.MessageType == PipeMessageType.HeartbeatAck)
            {
                lock (_heartbeatLock)
                {
                    _lastHeartbeatTime = DateTime.Now;
                }
            }
            else if (message.MessageType == PipeMessageType.PipeChanged)
            {
                _shouldReconnect = true;
                await Task.Delay(ReconnectDelay);
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

        /// <summary>
        /// 객체가 Dispose되었는지 확인하고, Dispose되었다면 예외를 발생시킵니다.
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(NamedPipeServer<T>));
            }
        }
    }
    
    public class CommunicationException : Exception
    {
        public CommunicationException(string message, Exception? innerException = null) 
            : base(message, innerException)
        {
        }
    }
}