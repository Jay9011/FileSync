using NamedPipeLine.Interfaces;
using NamedPipeLine.Services;
using S1FileSync.Models;

namespace S1FileSyncService.Services;

public class FileSyncIPCServer : IDisposable
{
    private const string PipeName = "S1FileSyncPipe";
    private readonly IIPCServer<FileSyncMessage> _server;
    private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
    private bool _isConnected;
    private readonly CancellationTokenSource _cancellationToken = new CancellationTokenSource();

    #region 의존 주입

    private readonly ILogger<FileSyncIPCServer> _logger;

    #endregion
    
    public event EventHandler<FileSyncMessage>? MessageReceived;
    
    public FileSyncIPCServer(ILogger<FileSyncIPCServer> logger)
    {
        #region 의존 주입

        _logger = logger;

        #endregion
        
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
        try
        {
            if (!_isConnected)
            {
                await RestartServerAsync(cancellationToken);
            }

            await _server.SendMessageAsync(message);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred while sending a message");
            _isConnected = false;
            throw;
        }
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
        if (_isConnected)
        {
            return;
        }

        try
        {
            _logger.LogInformation("Restarting the IPC server...");
            await _server.StopAsync();
            await Task.Delay(1000, cancellationToken);
            await StartAsync(cancellationToken);
            _logger.LogInformation("IPC server restarted successfully");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to restart the IPC server");
            throw;
        }
    }
}