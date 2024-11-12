using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using NamedPipeLine.Interfaces;
using NamedPipeLine.Services;
using S1FileSync.Models;
using S1FileSync.Services.Interface;
using S1FileSync.ViewModels;

namespace S1FileSync.Services;

public class FileSyncIPCClient : IDisposable
{
    private string _ipcStatus = "Disconnected";

    public string IPCStatus
    {
        get { return _ipcStatus; }
        private set
        {
            _ipcStatus = value;
            OnStatusChanged?.Invoke();
        }
    }
    
    private const string PipeName = "S1FileSyncPipe";
    private readonly IIPCClient<FileSyncMessage> _client;
    private readonly Dispatcher _UIDispatcher;

    #region 의존 주입

    private readonly ILogger<FileSyncIPCClient> _logger;
    private readonly ITrayIconService _trayIconService;
    private readonly FileSyncProgressViewModel _progressViewModel;

    #endregion
    
    /// <summary>
    /// 상태 변경 이벤트 연결
    /// </summary>
    public Action OnStatusChanged;

    public FileSyncIPCClient(ILogger<FileSyncIPCClient> logger, ITrayIconService trayIconService, FileSyncProgressViewModel progressViewModel)
    {
        #region 의존 주입

        _logger = logger;
        _trayIconService = trayIconService;
        _progressViewModel = progressViewModel;

        #endregion

        _client = new NamedPipeClient<FileSyncMessage>(PipeName);
        _client.MessageReceived += OnMessageReceived;
        _client.ConnectionStateChanged += OnConnectionStateChanged;

        _UIDispatcher = Application.Current.Dispatcher;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.ConnectAsync(cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to start the IPC client");
            throw;
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
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occurred while processing the message");
        }
    }

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        IPCStatus = e.IsConnected ? "Connected" : $"Disconnected: {(e.ErrorMessage != null ? $" ({e.ErrorMessage})" : "")}";
    }

    private void UpdateProgress(FileSyncProgress progress)
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
    
    public void Dispose()
    {
        if (_client is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}