using System.Security.Cryptography;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Input;
using S1FileSync.Helpers;
using S1FileSync.Services;
using S1FileSync.Services.Interface;

namespace S1FileSync.ViewModels;

public class MainViewModel : ViewModelBase
{
    #region 의존 주입

    private readonly IServiceControlService _serviceControlService;
    private readonly IPopupService _popupService;
    private readonly FileSyncIPCClient _ipcClient;

    #endregion
    
    private bool _isServiceRunning;
    public bool IsServiceRunning
    {
        get => _isServiceRunning;
        set => SetField(ref _isServiceRunning, value);
    }
    
    private string _serviceStatus = "Unknown";
    public string ServiceStatus
    {
        get => _serviceStatus;
        set => SetField(ref _serviceStatus, value);
    }
    
    private string _ipcStatus = "Disconnected";
    public string IPCStatus
    {
        get => _ipcStatus;
        private set => SetField(ref _ipcStatus, value);
    }
    
    public ICommand StartSyncCommand { get; set; }
    public ICommand StopSyncCommand { get; set; }
    public ICommand CheckStatusCommand { get; set; }

    public MainViewModel(IServiceControlService serviceControlService, IPopupService popupService, FileSyncIPCClient ipcClient)
    {
        #region 의존 주입

        _serviceControlService = serviceControlService;
        _popupService = popupService;
        _ipcClient = ipcClient;

        #endregion
        
        StartSyncCommand = new RelayCommand(async () => await StartSync());
        StopSyncCommand = new RelayCommand(async () => await StopSync());
        CheckStatusCommand = new RelayCommand(async () => await CheckServiceStatus());
        
        _ipcClient.ConnectionStateChanged += OnIPCConnectionStateChanged;
        _ = CheckServiceStatus();
    }

    /// <summary>
    /// 동기화 시작시 실행되는 이벤트 메서드
    /// </summary>
    private async Task StartSync()
    {
        try
        {
            await _serviceControlService.StartServiceAsync();
            await CheckServiceStatus();
        }
        catch (Exception e)
        {
            ServiceStatus = $"Error: {e.Message}";
        }
    }

    /// <summary>
    /// 동기화 종료시 실행되는 이벤트 메서드
    /// </summary>
    private async Task StopSync()
    {
        try
        {
            await _serviceControlService.StopServiceAsync();
            await CheckServiceStatus();
        }
        catch (Exception e)
        {
            ServiceStatus = $"Error: {e.Message}";
        }
    }
    
    /// <summary>
    /// 서비스 상태 확인시 실행되는 이벤트 메서드
    /// </summary>
    private async Task CheckServiceStatus()
    {
        try
        {
            var status = await _serviceControlService.GetServiceStatusAsync();
            IsServiceRunning = status == ServiceControllerStatus.Running;
            ServiceStatus = status?.ToString() ?? "Not found";
        }
        catch (Exception e)
        {
            ServiceStatus = $"Error: {e.Message}";
            IsServiceRunning = false;
        }
    }
    
    /// <summary>
    /// IPC 연결 상태 변경시 실행되는 이벤트 메서드
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="status"></param>
    private void OnIPCConnectionStateChanged(object? sender, FileSyncIPCClient.ConnectionStatus status)
    {
        IPCStatus = status.IsConnected ? "Connected" : $"Disconnected: {(status.ErrorMessage != null ? $" ({status.ErrorMessage})" : "")}";
    }

}