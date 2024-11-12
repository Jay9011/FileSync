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

        #region Connection

        private const int MaxRetryAttempts = 3;
        private const int RetryDelay = 1000;
        private const int MaxRetryDelay = 10000;
        private const int ConnectionTimeout = 5000;

        #endregion
        
        #region Heartbeat

        private readonly Timer _heartbeatCheckTimer;
        private readonly object _heartbeatLock = new object();
        private DateTime _lastHeartbeatTime = DateTime.MinValue;
        private const int HeartbeatCheckInterval = 1000;
        private const int HeartbeatTimeout = 5000;

        #endregion

        public bool IsConnected
        {
            get
            {
                if (_isDisposed || !_isConnected || _pipeClientStream == null)
                {
                    return false;
                }

                return _pipeClientStream.IsConnected;
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
            
            _heartbeatCheckTimer = new Timer(HeartbeatCheck, null, HeartbeatCheckInterval, HeartbeatCheckInterval);
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (_isRunning && IsConnected)
            {
                return;
            }
            
            await _connectionLock.WaitAsync(cancellationToken);
            try
            {
                int currentDelay = RetryDelay;
                int attemptCount = 0;

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await CreateNewConnectionAsync(cancellationToken);
                        
                        IsConnected = true;
                        _isRunning = true;
                        
                        _messageListenerTask = StartListeningAsync(cancellationToken);
                        _heartbeatCheckTimer.Change(0, HeartbeatCheckInterval);
                        return;
                    }
                    catch (Exception) when(!cancellationToken.IsCancellationRequested)
                    {
                        attemptCount++;
                        
                        await Task.Delay(currentDelay, cancellationToken);

                        if (currentDelay >= MaxRetryDelay)
                        {
                            currentDelay = RetryDelay;
                        }
                        else
                        {
                            currentDelay = Math.Min(currentDelay * 2, MaxRetryDelay);                            
                        }
                    }
                }
                throw new CommunicationException("Failed to connect to the server.");
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task SendMessageAsync(T message)
        {
            ThrowIfDisposed();

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            for (int attempt = 0; attempt < MaxRetryAttempts; attempt++)
            {
                await _messageLock.WaitAsync();
                
                try
                {
                    if (!IsValidPipeState())
                    {
                        if (attempt == MaxRetryAttempts - 1)
                        {
                            throw new InvalidOperationException("Pipe is not connected.");
                        }

                        await ReconnectAsync();
                        continue;
                    }
                    
                    string messageText = _serializer.Serialize(message);
                    await _writer.WriteLineAsync(messageText);
                    await _writer.FlushAsync();
                    return;
                }
                catch (Exception) when (attempt < MaxRetryAttempts - 1)
                {
                    await Task.Delay(RetryDelay * (attempt + 1));
                    await ReconnectAsync();
                }
                finally
                {
                    _messageLock.Release();
                }
            }
            throw new CommunicationException("Failed to send message.");
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
                _heartbeatCheckTimer.Change(Timeout.Infinite, Timeout.Infinite);
                
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
                _heartbeatCheckTimer.Dispose();
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

                lock (_heartbeatLock)
                {
                    _lastHeartbeatTime = DateTime.Now;
                }
            }
            catch (Exception e)
            {
                await CleanupConnectionAsync();
                throw;
            }
        }

        /// <summary>
        /// 연결 재시도. 만약 연결된 상태라면 무시합니다.
        /// </summary>
        private async Task ReconnectAsync()
        {
            if (IsConnected)
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
                        await Task.Delay(1000, linkedCancellationToken.Token);
                        await ReconnectAsync();
                        continue;
                    }
                    
                    string? messageText = await _reader.ReadLineAsync();
                    if (messageText == null)
                    {
                        await ReconnectAsync();
                        break;
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
                    await ReconnectAsync();
                    await Task.Delay(RetryDelay, linkedCancellationToken.Token);
                    break;
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
            else
            {
                await HandleHeartbeatMessage(message);
            }
        }

        /// <summary>
        /// 연결된 파이프의 상태가 유효한지 확인하고, 유효하지 않다면 연결을 재시도합니다.
        /// </summary>
        /// <param name="state"></param>
        private async void HeartbeatCheck(object state)
        {
            if (!_isRunning || !IsConnected || _isDisposed)
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
                    await ReconnectAsync();
                }
            }
            catch (Exception)
            {
                await ReconnectAsync();
            }
        }

        /// <summary>
        /// pipe 시스템 메시지가 수신 된 경우, 해당 메시지를 처리합니다.
        /// </summary>
        /// <param name="message"></param>
        private async Task HandleHeartbeatMessage(T? message)
        {
            switch (message.MessageType)
            {
                case PipeMessageType.Heartbeat:
                {
                    T response = new T
                    {
                        MessageType = PipeMessageType.HeartbeatAck
                    };
                    
                    await SendMessageAsync(response);

                    lock (_heartbeatLock)
                    {
                        _lastHeartbeatTime = DateTime.Now;
                    }
                }
                    break;
                case PipeMessageType.HeartbeatAck:
                    break;
                case PipeMessageType.PipeChanged:
                {
                    await Task.Delay(RetryDelay);
                    await ReconnectAsync();
                }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
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
        
        private void OnConnectionStateChanged(bool isConnected, string? errorMessage = null)
        {
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(isConnected, errorMessage));
        }

    }
}