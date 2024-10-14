using System.Windows.Input;
using FileSyncApp.Core;
using S1FileSync.Helpers;
using S1FileSync.Models;

namespace S1FileSync.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly IRemoteConnectionService _connectionService;
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

    public ICommand SaveSettingsCommand { get; set; }
    public ICommand TestConnectionCommand { get; set; }

    public SettingsViewModel(IRemoteConnectionService connectionService)
    {
        _connectionService = connectionService;
        Settings = new SyncSettings();
        TestConnectionCommand = new RelayCommand(async () => await TestConnectionAsync());
        SaveSettingsCommand = new RelayCommand(SaveSettings);
    }

    /// <summary>
    /// 저장시 실행되는 이벤트 함수
    /// </summary>
    private void SaveSettings()
    {
        // Save settings to file
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