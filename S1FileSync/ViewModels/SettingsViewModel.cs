using System.Windows;
using System.Windows.Input;
using FileIOService.Services.Interface;
using NetConnectionService;
using S1FileSync.Helpers;
using S1FileSync.Models;
using S1FileSync.Services.Interface;

namespace S1FileSync.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    #region 의존 주입

    private readonly IRemoteConnectionService _connectionService;
    private readonly IIniFileService _iniFileService;
    private readonly IPopupService _popupService;

    #endregion
    
    private SyncSettings _settings;
    private string _connectionStatus;

    public SyncSettings Settings
    {
        get => _settings;
        set => SetField(ref _settings, value);
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        set => SetField(ref _connectionStatus, value);
    }

    public string SyncIntervalString
    {
        get => Settings.SyncInterval;
        set
        {
            try
            {
                Settings.SyncInterval = value;
            }
            catch (ArgumentException e)
            {
                Settings.SyncInterval = ConstSettings.DefaultSyncInterval;
                _popupService.ShowMessage(e.Message, "Warning", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            OnPropertyChanged(nameof(SyncIntervalString));
        }
    }

    public ICommand SaveSettingsCommand { get; set; }
    public ICommand TestConnectionCommand { get; set; }
    public ICommand SyncSettingsCommand { get; set; }

    public SettingsViewModel(IRemoteConnectionService connectionService, IPopupService popupService, IIniFileService iniFileService)
    {
        #region 의존 주입

        _connectionService = connectionService;
        _iniFileService = iniFileService;
        _popupService = popupService;

        #endregion
            
        Settings = new SyncSettings();
        LoadSettingsFromIni();
        
        TestConnectionCommand = new RelayCommand(async () => await TestConnectionAsync());
        SaveSettingsCommand = new RelayCommand(SaveSettings);
        SyncSettingsCommand = new RelayCommand(SyncSettings);
    }

    /// <summary>
    /// 저장시 실행되는 이벤트 함수
    /// </summary>
    private void SaveSettings()
    {
        _iniFileService.WriteValue(ConstSettings.Settings, ConstSettings.RemoteLocation, Settings.RemoteLocation);
        _iniFileService.WriteValue(ConstSettings.Settings, ConstSettings.LocalLocation, Settings.LocalLocation);
        _iniFileService.WriteValue(ConstSettings.Settings, ConstSettings.FileExtensions, Settings.FileExtensions);
        _iniFileService.WriteValue(ConstSettings.Settings, ConstSettings.SyncInterval, Settings.SyncInterval);
        
        _iniFileService.WriteValue(ConstSettings.Settings, ConstSettings.Username, Settings.Username);
        // TODO: 추후 암호화 처리
        _iniFileService.WriteValue(ConstSettings.Settings, ConstSettings.Password, Settings.Password);
    }
    
    /// <summary>
    /// ini 파일에서 설정을 불러오는 함수
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    private void LoadSettingsFromIni()
    {
        Settings.RemoteLocation = _iniFileService.ReadValue(ConstSettings.Settings, ConstSettings.RemoteLocation);
        Settings.LocalLocation = _iniFileService.ReadValue(ConstSettings.Settings, ConstSettings.LocalLocation);
        Settings.FileExtensions = _iniFileService.ReadValue(ConstSettings.Settings, ConstSettings.FileExtensions);
        Settings.SyncInterval = _iniFileService.ReadValue(ConstSettings.Settings, ConstSettings.SyncInterval);
        
        Settings.Username = _iniFileService.ReadValue(ConstSettings.Settings, ConstSettings.Username);
        // TODO: 추후 암호화 처리
        Settings.Password = _iniFileService.ReadValue(ConstSettings.Settings, ConstSettings.Password);
    }
    
    /// <summary>
    /// Settings 파일과 설정을 동기화 (ini 파일에서 직접 설정을 변경한 경우 사용)
    /// </summary>
    private void SyncSettings()
    {
        LoadSettingsFromIni();
    }
    
    /// <summary>
    /// 테스트 연결시 실행되는 이벤트 함수
    /// </summary>
    private async Task TestConnectionAsync()
    {
        ConnectionStatus = "Testing connection...";
        (bool, string) isConnected = await _connectionService.TestConnectionAsync(
            Settings.RemoteLocation,
            Settings.Username,
            Settings.Password);
        
        ConnectionStatus = isConnected.Item2;
    }
}