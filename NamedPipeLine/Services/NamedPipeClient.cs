using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using NamedPipeLine.Interfaces;
using NamedPipeLine.Models;

namespace NamedPipeLine.Services
{
    public class NamedPipeClient<T> : IIPCClient<T>, IDisposable where T : class, IIPCMessage, new()
    {
        public event EventHandler<T?>? MessageReceived;
        public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged; 

        private readonly string _pipeName;
        private readonly string _serverName;
        private const int ConnectionTimeout = 5000;
        
        private readonly IMessageSerializer _serializer;
        private readonly CancellationTokenSource _cancellationToken;
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _messageLock = new SemaphoreSlim(1, 1);

        private bool _isRunning;
        private bool _isConnected;
        private bool _isDisposed;
        
        private NamedPipeClientStream? _pipeClientStream;
        private Task? _messageListenerTask;
        private StreamWriter? _writer;
        private StreamReader? _reader;

        public bool IsConnected
        {
            get
            {
                if (!_isConnected)
                {
                    return false;
                }

                return IsValidPipeState();
            }

            private set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    OnConnectionStateChanged(value);
                }
            }
        }

        public NamedPipeClient(string pipeName, string serverName = ".", IMessageSerializer? serializer = null)
        {
            _pipeName = pipeName;
            _serverName = serverName;
            _serializer = serializer ?? new JsonMessageSerializer();
            _cancellationToken = new CancellationTokenSource();
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            await _connectionLock.WaitAsync(cancellationToken);
            
            if (_isRunning && IsConnected)
            {
                _connectionLock.Release();
                return;
            }
            
            try
            {
                await CreateNewConnectionAsync(cancellationToken);
                
                _messageListenerTask = StartListeningAsync(cancellationToken);
            }
            catch (Exception e)
            {
                throw;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task SendMessageAsync(T message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            await _messageLock.WaitAsync();
            try
            {
                await ReconnectAsync();

                string messageText = _serializer.Serialize(message);
                await _writer.WriteLineAsync(messageText);
                await _writer.FlushAsync();
            }
            catch (PipeCommunicationException e) when (e.FailureType == PipeCommunicationFailureType.ConnectionFailed)
            {
                throw;
            }
            catch (Exception e)
            {
                await CleanupConnectionAsync();
                throw new PipeCommunicationException("Failed to send message to the server.", PipeCommunicationFailureType.SendFailed, e);
            }
            finally
            {
                _messageLock.Release();
            }
        }

        public async Task DisconnectAsync()
        {
            if (!_isRunning && !IsConnected)
            {
                return;
            }
            
            await _connectionLock.WaitAsync();
            try
            {
                _isRunning = false;
                IsConnected = false;
                
                await CleanupConnectionAsync();

                if (_messageListenerTask != null)
                {
                    try
                    {
                        await _messageListenerTask;
                    }
                    catch (OperationCanceledException)
                    {
                        // Ignore
                    }
                }
            }
            finally
            {
                _connectionLock.Release();
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
                IsConnected = false;
                _cancellationToken.Cancel();
                _connectionLock.Dispose();
                _messageLock.Dispose();
                CleanupConnectionAsync().Wait();
                _cancellationToken.Dispose();
            }
            
            _isDisposed = true;
        }
        
        /// <summary>
        /// 새로운 연결을 생성합니다.
        /// 만약, 연결이 이미 존재한다면 연결을 종료하고 새로운 연결을 생성합니다.
        /// </summary>
        /// <param name="cancellationToken"></param>
        private async Task CreateNewConnectionAsync(CancellationToken cancellationToken)
        {
            await CleanupConnectionAsync();
            
            _pipeClientStream = new NamedPipeClientStream(_serverName, _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            
            using var timeoutToken = new CancellationTokenSource(ConnectionTimeout);
            using var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(timeoutToken.Token, cancellationToken);

            try
            {
                await _pipeClientStream.ConnectAsync(linkedToken.Token);
                _writer = new StreamWriter(_pipeClientStream) { AutoFlush = true };
                _reader = new StreamReader(_pipeClientStream);
                
                IsConnected = true;
                _isRunning = true;
            }
            catch (Exception e)
            {
                await CleanupConnectionAsync();
                throw new PipeCommunicationException("Failed to connect to the server.", PipeCommunicationFailureType.ConnectionFailed, e);
            }
        }

        /// <summary>
        /// 연결 재시도. 만약 연결된 상태라면 무시합니다.
        /// </summary>
        private async Task ReconnectAsync()
        {
            if (IsConnected && IsValidPipeState())
            {
                return;
            }
            
            IsConnected = false;
            await ConnectAsync();
        }
        
        /// <summary>
        /// 메시지를 수신하고 처리합니다.
        /// 만약, 파이프가 연결되어 있지 않다면 연결을 재시도하고 메시지를 수신합니다.
        /// </summary>
        /// <param name="cancellationToken"></param>
        private async Task StartListeningAsync(CancellationToken cancellationToken)
        {
            using var linkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken.Token, cancellationToken);

            while (_isRunning && !linkedCancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!IsValidPipeState())
                    {
                        await ReconnectAsync();
                        continue;
                    }
                    
                    string? messageText = await _reader.ReadLineAsync();
                    if (messageText == null)
                    {
                        throw new InvalidOperationException("Received message is null.");
                    }

                    var message = _serializer.Deserialize<T>(messageText);
                    await MessageProcess(message);
                }
                catch (OperationCanceledException) when (linkedCancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception e)
                {
                    throw new PipeCommunicationException("Failed to receive message from the server.", PipeCommunicationFailureType.ReceiveFailed, e);
                }
            }
            
            IsConnected = false;
        }

        /// <summary>
        /// 메시지를 종류에 따라 처리합니다.
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

        /// <summary>
        /// 파이프 연결 상태가 유효한지 확인합니다.
        /// </summary>
        /// <returns></returns>
        private bool IsValidPipeState()
        {
            return _writer != null &&
                   _reader != null &&
                   _pipeClientStream != null &&
                   _pipeClientStream.IsConnected;
        }
        
        private async Task CleanupConnectionAsync()
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

            if (_pipeClientStream != null)
            {
                if (_pipeClientStream.IsConnected)
                {
                    _pipeClientStream.Close();
                }
                await _pipeClientStream.DisposeAsync();
                _pipeClientStream = null;
            }
            
            IsConnected = false;
            _isRunning = false;
            
            OnConnectionStateChanged(false);
        }
        
        private void OnConnectionStateChanged(bool isConnected, string? errorMessage = null)
        {
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(isConnected, errorMessage));
        }

    }
}