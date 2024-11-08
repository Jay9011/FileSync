using Microsoft.Extensions.Logging;
using NamedPipeLine.Interfaces;
using NamedPipeLine.Services;
using S1FileSync.Models;
using S1FileSync.Services.Interface;
using S1FileSync.ViewModels;

namespace S1FileSync.Services;

public class FileSyncIPCClient : IDisposable
{
    private const string PipeName = "S1FileSyncPipe";
    private readonly IIPCClient<FileSyncMessage> _client;
    private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
    private readonly CancellationTokenSource _cancellationToken = new CancellationTokenSource();

    #region 의존 주입

    private readonly ILogger<FileSyncIPCClient> _logger;
    private readonly ITrayIconService _trayIconService;
    private readonly FileSyncProgressViewModel _progressViewModel;

    #endregion

    #region 재시도 관련

    private const int MaxRetryAttempts = 3;
    private const int RetryDelayMs = 1000;
    private const int ReconnectIntervalMs = 5000;
    private bool _isConnected;

    #endregion
    
    public event EventHandler<ConnectionStatus>? ConnectionStateChanged;

    public record ConnectionStatus(bool IsConnected, string? ErrorMessage = null);

    public FileSyncIPCClient(ILogger<FileSyncIPCClient> logger, ITrayIconService trayIconService, FileSyncProgressViewModel progressViewModel)
    {
        #region 의존 주입

        _logger = logger;
        _trayIconService = trayIconService;
        _progressViewModel = progressViewModel;

        #endregion

        _client = new NamedPipeClient<FileSyncMessage>(PipeName);
        _client.MessageReceived += OnMessageReceived;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAsync(cancellationToken);
                ConnectionStateChanged?.Invoke(this, new ConnectionStatus(true));

                // 연결이 끊어질 때까지 대기
                while (_isConnected && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, cancellationToken);
                }

                ConnectionStateChanged?.Invoke(this, new ConnectionStatus(false));

                // 재연결 시도 전 대기
                if (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(ReconnectIntervalMs, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error occurred while starting the IPC client");
                ConnectionStateChanged?.Invoke(this, new ConnectionStatus(false, e.Message));
                await Task.Delay(ReconnectIntervalMs, cancellationToken);
            }
        }
    }

    private async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_isConnected)
            {
                return;
            }

            await _client.ConnectAsync(cancellationToken);
            _isConnected = true;
        }
        catch (Exception e)
        {
            _isConnected = false;
            _logger.LogError(e, "An error occurred while connecting to the IPC server");
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
                    await ConnectAsync(cancellationToken);
                }

                await _client.SendMessageAsync(message);
                return;
            }
            catch (Exception e)
            {
                lastException = e;
                _logger.LogError(e, $"Failed to send message to the IPC server. Retry attempt: {attempts + 1} / {MaxRetryAttempts}");
                
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
        ConnectionStateChanged?.Invoke(this, new ConnectionStatus(false, lastException?.Message));
        throw new CommunicationException("Failed to send message to the IPC server", lastException);
    }

    public void Dispose()
    {
        _cancellationToken.Cancel();
        _connectionLock.Dispose();
        if (_client is IDisposable disposable)
        {
            disposable.Dispose();
        }
        _cancellationToken.Dispose();
    }
    
    private void OnMessageReceived(object? sender, FileSyncMessage message)
    {
        try
        {
            switch (message.Content?.Type)
            {
                case FileSyncMessageType.ProgressUpdate:
                {
                    if (message.Content.Progress != null)
                    {
                        UpdateProgress(message.Content.Progress);
                    }
                }
                    break;
                case FileSyncMessageType.StatusChange:
                {
                    if (Enum.TryParse<TrayIconStatus>(message.Content.Message, out var status))
                    {
                        _trayIconService.SetStatus(status);
                    }
                }
                    break;
                case FileSyncMessageType.Error:
                {
                    _trayIconService.SetStatus(TrayIconStatus.Error);
                }
                    break;
                case FileSyncMessageType.ServiceCommand:
                    break;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occurred while processing the received message");
        }
    }
    
    private void UpdateProgress(FileSyncProgress progress)
    {
        try
        {
            var item = _progressViewModel.AddOrUpdateItem(progress.FileName, progress.FileSize);
            if (item != null)
            {
                item.Progress = progress.Progress;
                item.SyncSpeed = progress.Speed;
                item.IsCompleted = progress.IsCompleted;

                if (progress.IsCompleted)
                {
                    item.LastSyncTime = DateTime.Now;
                }
            }
            
            _progressViewModel.RemoveCompletedItems();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occurred while updating the progress");
        }
    }

    public class CommunicationException : Exception
    {
        public CommunicationException(string message, Exception? innerException = null) : base(message, innerException)
        {
        }
    }
}