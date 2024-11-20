using System.ServiceProcess;
using System.Windows;
using System.Windows.Input;
using S1FileSync.Helpers;
using S1FileSync.Services;
using S1FileSync.Services.Interface;
using S1FileSync.Views;

namespace S1FileSync.ViewModels;

public class MainViewModel : ViewModelBase, IDisposable
{
    #region 의존 주입

    public required SyncMonitorView MonitorView { get; set; }
    public required SettingsView SettingsView { get; set; }
    public required FileSyncProgressView ProgressView { get; set; }
    
    private readonly IRemoteServerConnectionChecker _remoteServerConnectionChecker;
    private readonly IServiceControlService _serviceControlService;
    private readonly FileSyncIPCClient _ipcClient;
    private readonly ITrayIconService _trayIconService;
    private readonly IPopupService _popupService;

    #endregion

    public const double SidebarExpandedWidth = 240;
    public const double SidebarCollapsedWidth = 60;
    public static GridLength SidebarIconColumnWidth = new GridLength(28);

    private readonly PeriodicTimer _monitorTimer;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Task? _monitorTask;
    
    private bool _isDarkTheme = true;
    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set => SetField(ref _isDarkTheme, value);
    }

    private double _sidebarWidth = SidebarExpandedWidth;
    public double SidebarWidth
    {
        get => _sidebarWidth;
        set => SetField(ref _sidebarWidth, value); 
    }
    
    private bool _isSidebarExpanded = true;

    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded; 
        set
        {
            if (SetField(ref _isSidebarExpanded, value))
            {
                SidebarWidth = value ? SidebarExpandedWidth : SidebarCollapsedWidth;
            }
        }
    }
    
    private bool _isServiceRunning;
    public bool IsServiceRunning
    {
        get => _isServiceRunning;
        set => SetField(ref _isServiceRunning, value);
    }

    private bool _isServiceStopped;

    public bool IsServiceStopped
    {
        get => _isServiceStopped;
        set => SetField(ref _isServiceStopped, value);
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
        set => SetField(ref _ipcStatus, value);
    }
    
    public bool IsConnected
    {
        get => _remoteServerConnectionChecker.RemoteServerConnected;
        set => _remoteServerConnectionChecker.RemoteServerConnected = value;
    }
    
    public string ConnectionStatus
    {
        get => _remoteServerConnectionChecker.RemoteServerConnectionStatus;
        set => _remoteServerConnectionChecker.RemoteServerConnectionStatus = value;
    }

    public string _getConnected = "Unknown";
    public string GetConnected
    {
        get => _getConnected;
        set => SetField(ref _getConnected, value);
    }

    public bool _getConnectedStatus;
    public bool GetConnectedStatus
    {
        get => _getConnectedStatus;
        set => SetField(ref _getConnectedStatus, value);
    }


    public ICommand StartSyncCommand { get; set; }
    public ICommand StopSyncCommand { get; set; }
    public ICommand ClearItemList { get; set; }
    public ICommand CheckStatusCommand { get; set; }
    

    public MainViewModel(IServiceControlService serviceControlService, IPopupService popupService, FileSyncIPCClient ipcClient, IRemoteServerConnectionChecker remoteServerConnectionChecker, ITrayIconService trayIconService)
    {
        #region 의존 주입

        _serviceControlService = serviceControlService;
        _popupService = popupService;
        _ipcClient = ipcClient;
        _remoteServerConnectionChecker = remoteServerConnectionChecker;
        _trayIconService = trayIconService;

        #endregion
        
        StartSyncCommand = new RelayCommand(async () => await StartSync());
        StopSyncCommand = new RelayCommand(async () => await StopSync());
        CheckStatusCommand = new RelayCommand(async () => await CheckServiceStatus());
        ClearItemList = new RelayCommand(async () => await ItemListClear());

        _monitorTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        _monitorTask = MonitorService();
    }

    private async Task MonitorService()
    {
        try
        {
            while (await _monitorTimer.WaitForNextTickAsync(_cancellationTokenSource.Token))
            {
                await CheckServiceStatus();
                if (!IsServiceRunning)
                {
                    IsConnected = false;
                }
                await SetGetConnected();
            }
        }
        catch (Exception e)
        {
            ServiceStatus = $"Error: {e.Message}";
        }
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
    /// 동기화 화면의 ItemList를 초기화하는 이벤트 메서드
    /// </summary>
    /// <returns></returns>
    private async Task ItemListClear()
    {
        ProgressView.ProgressListClear();
    }

    /// <summary>
    /// 서비스 상태 확인시 실행되는 이벤트 메서드
    /// </summary>
    private async Task CheckServiceStatus()
    {
        try
        {
            var status = await _serviceControlService.GetServiceStatusAsync();

            if (status == ServiceControllerStatus.Stopped)
            {
                IsServiceStopped = true;
            }
            else
            {
                IsServiceStopped = false;
            }

            if (status != ServiceControllerStatus.Running)
            {
                StoppingServiceProgress();
            }
            else if (!IsServiceRunning && 
                     status == ServiceControllerStatus.Running)
            {
                RunningServiceProgress();
            }

            IsServiceRunning = status == ServiceControllerStatus.Running;
            ServiceStatus = status?.ToString() ?? "Not found";
        }
        catch (Exception e)
        {
            ServiceStatus = $"Error: {e.Message}";
            IsServiceRunning = false;
        }
    }

    private async Task SetGetConnected()
    {
        GetConnectedStatus = false;
        IPCStatus = _ipcClient.IPCStatus;

        if (!IsServiceRunning)
        {
            GetConnected = ServiceStatus;
            await _ipcClient.StopAsync();
            IPCStatus = _ipcClient.IPCStatus;
            return;
        }
        else if (!IsConnected)
        {
            GetConnected = ConnectionStatus;
            return;
        }

        GetConnected = "Connected";
        GetConnectedStatus = true;
    }

    private void StoppingServiceProgress()
    {
        if (_trayIconService.GetStatus() != TrayIconStatus.Stop)
        {
            _trayIconService.SetStatus(TrayIconStatus.Stop);
        }
    }

    private void RunningServiceProgress()
    {
        if (_trayIconService.GetStatus() == TrayIconStatus.Stop ||
            _trayIconService.GetStatus() == TrayIconStatus.Error)
        {
            _trayIconService.SetStatus(TrayIconStatus.Normal);
        }
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}