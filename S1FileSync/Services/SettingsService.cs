using FileIOHelper;
using S1FileSync.Models;
using S1FileSync.Services.Interface;

namespace S1FileSync.Services;

public class SettingsService : ISettingsService
{
    private readonly IIniFileHelper _iniFileHelper;
    
    public SettingsService(IIniFileHelper iniFileHelper)
    {
        _iniFileHelper = iniFileHelper;
    }
    
    public SyncSettings LoadSettings()
    {
        if (_iniFileHelper is IFileIOHelper fileIoHelper)
        {
            if (!fileIoHelper.FileExists(_iniFileHelper.GetFilePath()))
            {
                SaveSettings(new SyncSettings());
            }
        }
        
        return new SyncSettings
        {
            RemoteLocation = _iniFileHelper.ReadValue(ConstSettings.Settings, ConstSettings.RemoteLocation),
            LocalLocation = _iniFileHelper.ReadValue(ConstSettings.Settings, ConstSettings.LocalLocation),
            FileExtensions = _iniFileHelper.ReadValue(ConstSettings.Settings, ConstSettings.FileExtensions),
            SyncInterval = _iniFileHelper.ReadValue(ConstSettings.Settings, ConstSettings.SyncInterval),

            Username = _iniFileHelper.ReadValue(ConstSettings.Settings, ConstSettings.Username),
            // TODO: 추후 암호화 처리
            Password = _iniFileHelper.ReadValue(ConstSettings.Settings, ConstSettings.Password)
        };
    }

    public void SaveSettings(SyncSettings settings)
    {
        _iniFileHelper.WriteValue(ConstSettings.Settings, ConstSettings.RemoteLocation, settings.RemoteLocation);
        _iniFileHelper.WriteValue(ConstSettings.Settings, ConstSettings.LocalLocation, settings.LocalLocation);
        _iniFileHelper.WriteValue(ConstSettings.Settings, ConstSettings.FileExtensions, settings.FileExtensions);
        _iniFileHelper.WriteValue(ConstSettings.Settings, ConstSettings.SyncInterval, settings.SyncInterval);
        
        _iniFileHelper.WriteValue(ConstSettings.Settings, ConstSettings.Username, settings.Username);
        // TODO: 추후 암호화 처리
        _iniFileHelper.WriteValue(ConstSettings.Settings, ConstSettings.Password, settings.Password);
    }

    public void SyncSettings()
    {
        LoadSettings();
    }
}