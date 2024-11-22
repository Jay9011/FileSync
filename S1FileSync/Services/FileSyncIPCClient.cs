using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using NamedPipeLine.Interfaces;
using NamedPipeLine.Services;
using S1FileSync.Models;
using S1FileSync.Services.Interface;
using S1FileSync.ViewModels;

namespace S1FileSync.Services;

public class FileSyncIPCClient : PropertyChangeNotifier, IDisposable
{
    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        private set => SetField(ref _isConnected, value);
    }
    
    private string _ipcStatus = "Disconnected";
    public string IPCStatus
    {
        get { return _ipcStatus; }
        private set => SetField(ref _ipcStatus, value);
    }
    
    private const string PipeName = "S1FileSyncPipe";
    private readonly IIPCClient<FileSyncMessage> _client;
    private readonly Dispatcher _UIDispatcher;
    private readonly PeriodicTimer _connectionMonitorTimer;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Task? _monitorTask;

    #region 의존 주입

    private readonly ILogger<FileSyncIPCClient> _logger;
    private readonly ITrayIconService _trayIconService;
    private readonly FileSyncProgressViewModel _progressViewModel;
    private readonly IRemoteServerConnectionChecker _remoteServerConnectionChecker;

    #endregion
    
    public FileSyncIPCClient(ILogger<FileSyncIPCClient> logger, ITrayIconService trayIconService, FileSyncProgressViewModel progressViewModel, IRemoteServerConnectionChecker remoteServerConnectionChecker)
    {
        #region 의존 주입

        _logger = logger;
        _trayIconService = trayIconService;
        _progressViewModel = progressViewModel;
        _remoteServerConnectionChecker = remoteServerConnectionChecker;

        #endregion

        _client = new NamedPipeClient<FileSyncMessage>(PipeName);
        _client.MessageReceived += OnMessageReceived;
        _client.ConnectAsync();

        _UIDispatcher = Application.Current.Dispatcher;

        _connectionMonitorTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        StartConnectionMonitoring();
    }

    /// <summary>
    /// 서버에 연결
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_client.IsConnected)
            {
                await _client.ConnectAsync(cancellationToken);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to start the IPC client");
            throw;
        }
    }
    
    /// <summary>
    /// 서버와의 연결 해제
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_client.IsConnected)
            {
                await _client.DisconnectAsync();
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to stop the IPC client");
            IsConnected = _client.IsConnected;
            if (!IsConnected)
            {
                IPCStatus = "Disconnected";
            }
        }
    }

    /// <summary>
    /// 메시지 수신 이벤트
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="message"></param>
    private void OnMessageReceived(object? sender, FileSyncMessage message)
    {
        if (message?.Content == null)
        {
            return;
        }
        
        try
        {
            _UIDispatcher.BeginInvoke(new Action(() => ProcessMessage(message)));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occurred while processing the received message");
        }
    }
    
    /// <summary>
    /// 메시지 처리
    /// </summary>
    /// <param name="message"></param>
    private void ProcessMessage(FileSyncMessage message)
    {
        try
        {
            switch (message.Content.Type)
            {
                case FileSyncMessageType.ProgressUpdate:
                {
                    if (message.Content.Progress != null)
                    {
                        _progressViewModel.UpdateProgress(message.Content.Progress);
                    }
                }
                    break;
                case FileSyncMessageType.StatusChange:
                {
                    ProgramStatusChange(message.Content);
                }
                    break;
                case FileSyncMessageType.ConnectionStatus:
                {
                    var connectionStatusType = message.GetConnectionStatusType();
                    _remoteServerConnectionChecker.ConnectionChange(connectionStatusType == ConnectionStatusType.Connected, message.GetConnectionStatusMessage());
                }
                    break;
                case FileSyncMessageType.Error:
                {
                    _trayIconService.SetStatus(TrayIconStatus.Error);
                }
                    break;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occurred while processing the message");
        }
    }

    /// <summary>
    /// 연결 상태 모니터링 시작
    /// </summary>
    private void StartConnectionMonitoring()
    {
        _monitorTask = MonitorConnectionAsync();
    }

    /// <summary>
    /// 모니터링 Task
    /// </summary>
    private async Task MonitorConnectionAsync()
    {
        try
        {
            do
            {
                try
                {
                    if (_client.IsConnected)
                    {
                        IsConnected = true;
                        IPCStatus = "Connected";
                    }
                    else
                    {
                        _logger.LogWarning("IPC connection is lost. Reconnecting...");
                        IsConnected = false;
                        IPCStatus = "Disconnected";
                        await _client.ConnectAsync(_cancellationTokenSource.Token);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error occurred while monitoring the connection");
                    IsConnected = false;
                    IPCStatus = $"Connection error: {e.Message}";
                }
            } while (await _connectionMonitorTimer.WaitForNextTickAsync(_cancellationTokenSource.Token));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Connection monitoring task is stopped");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occurred while monitoring the connection");
        }
    }

    public async Task SendMessageAsync(FileSyncMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.SendMessageAsync(message);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to send a message");
        }
    }
    
    /// <summary>
    /// 프로그램 상태 변경
    /// </summary>
    /// <param name="content"></param>
    private void ProgramStatusChange(FileSyncContent content)
    {
        if (Enum.TryParse<TrayIconStatus>(content.Message, out var status))
        {
            _trayIconService.SetStatus(status);
        }
        else if (Enum.TryParse<FileSyncStatusType>(content.Message, out var syncStatus))
        {
            switch (syncStatus)
            {
                case FileSyncStatusType.SyncStart:
                    _trayIconService.SetStatus(TrayIconStatus.Syncing);
                    break;
                case FileSyncStatusType.SyncEnd:
                    break;
                case FileSyncStatusType.SyncError:
                    break;
            }
        }
    }
    
    public void Dispose()
    {
        try
        {
            _cancellationTokenSource.Cancel();
            _monitorTask?.Wait(TimeSpan.FromSeconds(1));
            _connectionMonitorTimer.Dispose();
            _cancellationTokenSource.Dispose();
        }
        finally
        {
            if (_client is IDisposable disposable)
            {
                disposable.Dispose();
            }            
        }
    }
}