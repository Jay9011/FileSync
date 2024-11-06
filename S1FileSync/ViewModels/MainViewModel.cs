using System.Security.Cryptography;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Input;
using S1FileSync.Helpers;
using S1FileSync.Services.Interface;

namespace S1FileSync.ViewModels;

public class MainViewModel : ViewModelBase
{
    #region 의존 주입

    private readonly IServiceControlService _serviceControlService;
    private readonly IPopupService _popupService;

    #endregion
    
    private bool _isServiceRunning;
    private string _serviceStatus = "Unknown";

    public bool IsServiceRunning
    {
        get => _isServiceRunning;
        set => SetField(ref _isServiceRunning, value);
    }
    
    public string ServiceStatus
    {
        get => _serviceStatus;
        set => SetField(ref _serviceStatus, value);
    }
    
    public ICommand StartSyncCommand { get; set; }
    public ICommand StopSyncCommand { get; set; }
    public ICommand CheckStatusCommand { get; set; }

    public MainViewModel(IServiceControlService serviceControlService, IPopupService popupService)
    {
        #region 의존 주입

        _serviceControlService = serviceControlService;
        _popupService = popupService;

        #endregion
        
        StartSyncCommand = new RelayCommand(async () => await StartSync());
        StopSyncCommand = new RelayCommand(async () => await StopSync());
        CheckStatusCommand = new RelayCommand(async () => await CheckServiceStatus());
        
        _ = CheckServiceStatus();
    }

    /// <summary>
    /// 동기화 시작시 실행되는 이벤트 메서드
    /// </summary>
    private async Task StartSync()
    {
        var result = await _serviceControlService.StartServiceAsync();
        if (!result.success)
        {
            _popupService.ShowMessage(result.message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        await CheckServiceStatus();
    }

    /// <summary>
    /// 동기화 종료시 실행되는 이벤트 메서드
    /// </summary>
    private async Task StopSync()
    {
        var result = await _serviceControlService.StopServiceAsync();
        if (!result.success)
        {
            _popupService.ShowMessage(result.message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        await CheckServiceStatus();
    }
    
    /// <summary>
    /// 서비스 상태 확인시 실행되는 이벤트 메서드
    /// </summary>
    private async Task CheckServiceStatus()
    {
        var status = await _serviceControlService.GetServiceStatusAsync();
        IsServiceRunning = status == ServiceControllerStatus.Running;
        ServiceStatus = status?.ToString() ?? "Not found";
    }
}