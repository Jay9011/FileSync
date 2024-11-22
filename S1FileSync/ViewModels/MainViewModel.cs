using System.ServiceProcess;
using System.Windows;
using System.Windows.Input;
using S1FileSync.Helpers;
using S1FileSync.Services;
using S1FileSync.Services.Interface;
using S1FileSync.Views;

namespace S1FileSync.ViewModels;

public class MainViewModel : PropertyChangeNotifier, IDisposable
{
    #region 의존 주입

    public required SyncMonitorView MonitorView { get; set; }
    public required SettingsView SettingsView { get; set; }
    
    public IServiceControlService ServiceControlService { get; private set; }
    public FileSyncIPCClient IpcClient { get; private set; }
    
    private readonly ITrayIconService _trayIconService;

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
    
    public ICommand StartSyncCommand { get; set; }
    public ICommand StopSyncCommand { get; set; }
    
    public MainViewModel(IServiceControlService serviceControlService, FileSyncIPCClient ipcClient, ITrayIconService trayIconService)
    {
        #region 의존 주입

        ServiceControlService = serviceControlService;
        IpcClient = ipcClient;
        _trayIconService = trayIconService;

        #endregion
        
        StartSyncCommand = new RelayCommand(async () => await StartSync(),
            canExecute: () => (ServiceControlService.Status == ServiceControllerStatus.Stopped));
        StopSyncCommand = new RelayCommand(async () => await StopSync(),
            canExecute: () => (ServiceControlService.Status == ServiceControllerStatus.Running));
        
        _monitorTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        _monitorTask = MonitorService();
    }

    /// <summary>
    /// 서비스 모니터링
    /// </summary>
    private async Task MonitorService()
    {
        try
        {
            while (await _monitorTimer.WaitForNextTickAsync(_cancellationTokenSource.Token))
            {
                await CheckServiceStatus();
            }
        }
        catch (Exception e)
        {
            ServiceControlService.StatusMessage = $"Error: {e.Message}";
        }
    }

    /// <summary>
    /// 동기화 시작시 실행되는 이벤트 메서드
    /// </summary>
    private async Task StartSync()
    {
        try
        {
            await ServiceControlService.StartServiceAsync();
        }
        catch (Exception e)
        {
            ServiceControlService.StatusMessage = $"Error: {e.Message}";
        }
    }

    /// <summary>
    /// 동기화 종료시 실행되는 이벤트 메서드
    /// </summary>
    private async Task StopSync()
    {
        try
        {
            await ServiceControlService.StopServiceAsync();
        }
        catch (Exception e)
        {
            ServiceControlService.StatusMessage = $"Error: {e.Message}";
        }
    }

    /// <summary>
    /// 서비스 상태 확인시 실행되는 이벤트 메서드
    /// </summary>
    private async Task CheckServiceStatus()
    {
        try
        {
            var status = await ServiceControlService.GetServiceStatusAsync();

            if (status == ServiceControllerStatus.Running)
            {
                RunningServiceProgress();
            }
            else
            {
                StoppingServiceProgress();
            }
        }
        catch (Exception e)
        {
            ServiceControlService.StatusMessage = $"Error: {e.Message}";
        }
    }
    
    /// <summary>
    /// 서비스 중단시 처리해야 할 작업
    /// </summary>
    private void StoppingServiceProgress()
    {
        if (_trayIconService.GetStatus() != TrayIconStatus.Stop)
        {
            _trayIconService.SetStatus(TrayIconStatus.Stop);
        }
        
        IpcClient.StopAsync(); // 서비스 중단시 IPC 연결 종료
    }

    /// <summary>
    /// 서비스 시작시 처리해야 할 작업
    /// </summary>
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
        try
        {
            _cancellationTokenSource.Cancel();
            _monitorTimer.Dispose();
            _monitorTask?.Wait(TimeSpan.FromSeconds(1));
            _monitorTask?.Dispose();
        }
        finally
        {
            _cancellationTokenSource.Dispose();
        }
    }
}