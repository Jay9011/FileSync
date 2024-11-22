using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FileIOHelper;
using NetConnectionHelper.Interface;
using S1FileSync.Helpers;
using S1FileSync.Models;
using S1FileSync.Services.Interface;

namespace S1FileSync.ViewModels;

public class SettingsViewModel : PropertyChangeNotifier
{
    #region 의존 주입

    private readonly IRemoteConnectionHelper _connectionHelper;
    private readonly ISettingsService _settingsService;
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
    public ICommand TestSyncCommand { get; set; }

    public class ComboOption
    {
        public string DisplayText { get; set; }
        public object Value { get; set; }
    }
    
    public IEnumerable<ComboOption> FolderStructureOptions => new[]
    {
        new ComboOption { DisplayText = "Keep original folder structure", Value = false },
        new ComboOption { DisplayText = "Save all files in one folder", Value = true }
    };
    
    public IEnumerable<ComboOption> DuplicateHandlingOptions => new[]
    {
        new ComboOption { DisplayText = "Replace if different", Value = Models.SyncSettings.DuplicateFileHandling.ReplaceIfDifferent },
        new ComboOption { DisplayText = "Always replace", Value = Models.SyncSettings.DuplicateFileHandling.Replace },
        new ComboOption { DisplayText = "Skip", Value = Models.SyncSettings.DuplicateFileHandling.Skip },
        new ComboOption { DisplayText = "Keep both (rename)", Value = Models.SyncSettings.DuplicateFileHandling.KeepBoth }
    };

    public SettingsViewModel(IRemoteConnectionHelper connectionHelper, IPopupService popupService, ISettingsService settingsService)
    {
        #region 의존 주입

        _connectionHelper = connectionHelper;
        _popupService = popupService;
        _settingsService = settingsService;

        #endregion
            
        LoadSettings();
        
        TestConnectionCommand = new RelayCommand(async () => await TestConnectionAsync());
        SaveSettingsCommand = new RelayCommand(SaveSettings);
        SyncSettingsCommand = new RelayCommand(SyncSettings);
    }

    /// <summary>
    /// 저장시 실행되는 이벤트 함수
    /// </summary>
    private void SaveSettings()
    {
        if (string.IsNullOrEmpty(Settings.Password))
        {
            var existingSettings = _settingsService.LoadSettings();
            Settings.Password = existingSettings.Password;
        }
        
        _settingsService.SaveSettings(Settings);

        _popupService.ShowMessage("Settings saved successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    
    /// <summary>
    /// ini 파일에서 설정을 불러오는 함수
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    private void LoadSettings()
    {
        Settings = _settingsService.LoadSettings();
    }
    
    /// <summary>
    /// Settings 파일과 설정을 동기화 (ini 파일에서 직접 설정을 변경한 경우 사용)
    /// </summary>
    private void SyncSettings()
    {
        var currentPassword = Settings.Password;
        _settingsService.SyncSettings();
        LoadSettings();
        
        if (string.IsNullOrEmpty(Settings.Password))
        {
            Settings.Password = currentPassword;
        }
    }
    
    /// <summary>
    /// 테스트 연결시 실행되는 이벤트 함수
    /// </summary>
    private async Task TestConnectionAsync()
    {
        ConnectionStatus = "Testing connection...";
        (bool, string) isConnected = await _connectionHelper.ConnectionAsync(
            Settings.RemoteLocation,
            Settings.Username,
            Settings.Password);
        
        ConnectionStatus = isConnected.Item2;
    }
}