using NamedPipeLine.Interfaces;
using NamedPipeLine.Services;
using S1FileSync.Models;

namespace S1FileSyncService.Services;

public class FileSyncIPCServer : IDisposable
{
    private const string PipeName = "S1FileSyncPipe";
    private readonly IIPCServer<FileSyncMessage> _server;
    private readonly ILogger<FileSyncIPCServer> _logger;
    private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
    private bool _isConnected;
    private readonly CancellationTokenSource _cancellationToken = new CancellationTokenSource();
    
    #region 재시도 관련

    private const int MaxRetryAttempts = 3;
    private const int RetryDelayMs = 1000;

    #endregion
    
    public event EventHandler<FileSyncMessage>? MessageReceived;
    
    public FileSyncIPCServer(ILogger<FileSyncIPCServer> logger)
    {
        _logger = logger;
        _server = new NamedPipeServer<FileSyncMessage>(PipeName);
        _server.MessageReceived += OnMessageReceived;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_isConnected)
            {
                return;
            }

            await _server.StartAsync(cancellationToken);
            
            _isConnected = true;
            _logger.LogInformation("IPC server started successfully");
        }
        catch (Exception e)
        {
            _isConnected = false;
            _logger.LogError(e, "An error occurred while starting the IPC server");
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    
    public async Task SendMessageAsync(FileSyncMessage message, CancellationToken cancellationToken = default)
    {
        int attempts = 0;
        Exception? lastException = null;

        while (attempts < MaxRetryAttempts)
        {
            try
            {
                if (!_isConnected)
                {
                    await RestartServerAsync(cancellationToken);
                }

                await _server.SendMessageAsync(message);
                return;
            }
            catch (Exception e)
            {
                lastException = e;
                _logger.LogWarning(e, "An error occurred while sending a message");
                
                attempts++;
                if (attempts < MaxRetryAttempts)
                {
                    await Task.Delay(RetryDelayMs * attempts, cancellationToken);
                }
                
                _isConnected = false;
            }
        }
        
        // 재연결 실패
        _logger.LogError(lastException, "Failed to send message to the IPC server");
        throw new CommunicationException("Failed to send message to the IPC server", lastException);
    }

    public void Dispose()
    {
        _cancellationToken.Cancel();
        _connectionLock.Dispose();
        if (_server is IDisposable disposable)
        {
            disposable.Dispose();
        }
        _cancellationToken.Dispose();
    }
    
    private void OnMessageReceived(object? sender, FileSyncMessage message)
    {
        try
        {
            MessageReceived?.Invoke(this, message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error occurred while processing the received message");
        }
    }

    private async Task RestartServerAsync(CancellationToken cancellationToken)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_isConnected)
            {
                return;
            }

            await _server.StopAsync();
            await Task.Delay(1000, cancellationToken);
            await StartAsync(cancellationToken);
        }
        finally
        {
            _connectionLock.Release();
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