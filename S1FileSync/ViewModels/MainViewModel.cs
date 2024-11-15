using System.Security.Cryptography;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Input;
using S1FileSync.Helpers;
using S1FileSync.Services;
using S1FileSync.Services.Interface;
using S1FileSync.Views;

namespace S1FileSync.ViewModels;

public class MainViewModel : ViewModelBase
{
    #region 의존 주입

    public required SyncMonitorView MonitorView { get; set; }
    public required SettingsView SettingsView { get; set; }
    public required FileSyncProgressView ProgressView { get; set; }
    public required FileSyncProgressViewModel ProgressViewModel { get; set; }
    
    private readonly IServiceControlService _serviceControlService;
    private readonly IPopupService _popupService;
    private readonly FileSyncIPCClient _ipcClient;

    #endregion

    public const double SidebarExpandedWidth = 240;
    public const double SidebarCollapsedWidth = 60;
    public static GridLength SidebarIconColumnWidth = new GridLength(28);
    
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
    
    private bool _isConnected = false;
    public bool IsConnected
    {
        get => _isConnected;
        set => SetField(ref _isConnected, value);
    }
    
    private string _connectionStatus = "Disconnected";
    public string ConnectionStatus
    {
        get => _connectionStatus;
        set => SetField(ref _connectionStatus, value);
    }

    public string _getConnected = "Unknown";
    public string GetConnected
    {
        get => _getConnected;
        set => SetField(ref _getConnected, value);
    }

    public bool _getConnectedStatus = false;
    public bool GetConnectedStatus
    {
        get => _getConnectedStatus;
        set => SetField(ref _getConnectedStatus, value);
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
        
        _ = CheckServiceStatus();
        _ipcClient.OnStatusChanged += OnConnectionStateChanged;
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
    private void OnConnectionStateChanged()
    {
        IPCStatus = _ipcClient.IPCStatus;
        IsConnected = _ipcClient.Connected;
        ConnectionStatus = _ipcClient.ConnectionStatus;

        GetConnectedStatus = IsServiceRunning && IsConnected && !string.Equals(IPCStatus, "Disconnected");
        
        if (!IsConnected)
        {
            GetConnected = ConnectionStatus;
        }
        else if (!IsServiceRunning)
        {
            GetConnected = ServiceStatus;
        }
        else if (string.Equals(IPCStatus, "Disconnected"))
        {
            GetConnected = IPCStatus;
        }
        else if (IsConnected && IsServiceRunning)
        {
            GetConnected = "Connected";
        }
    }

}