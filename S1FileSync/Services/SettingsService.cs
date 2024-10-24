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

        var settings = new SyncSettings();
        settings.RemoteLocation = _iniFileHelper.ReadValue(ConstSettings.Settings, ConstSettings.RemoteLocation);
        settings.LocalLocation = _iniFileHelper.ReadValue(ConstSettings.Settings, ConstSettings.LocalLocation);
        settings.FolderPattern = _iniFileHelper.ReadValue(ConstSettings.Settings, ConstSettings.FolderPattern);
        settings.FileExtensions = _iniFileHelper.ReadValue(ConstSettings.Settings, ConstSettings.FileExtensions);
        settings.SyncInterval = _iniFileHelper.ReadValue(ConstSettings.Settings, ConstSettings.SyncInterval);
        
        string useFlatStructure = _iniFileHelper.ReadValue(ConstSettings.Settings, ConstSettings.UseFlatStructure);
        useFlatStructure = string.IsNullOrWhiteSpace(useFlatStructure) ? "False" : useFlatStructure;
        settings.UseFlatStructure = Convert.ToBoolean(useFlatStructure);
        
        string duplicateHandling = _iniFileHelper.ReadValue(ConstSettings.Settings, ConstSettings.DuplicateHandling);
        duplicateHandling = string.IsNullOrWhiteSpace(duplicateHandling) ? "0" : duplicateHandling;
        settings.DuplicateHandling = (SyncSettings.DuplicateFileHandling)Convert.ToInt32(duplicateHandling);
        
        settings.Username = _iniFileHelper.ReadValue(ConstSettings.Settings, ConstSettings.Username);
        // TODO: 추후 암호화 처리
        settings.Password = _iniFileHelper.ReadValue(ConstSettings.Settings, ConstSettings.Password);
        
        return settings;
    }

    public void SaveSettings(SyncSettings settings)
    {
        _iniFileHelper.WriteValue(ConstSettings.Settings, ConstSettings.RemoteLocation, settings.RemoteLocation);
        _iniFileHelper.WriteValue(ConstSettings.Settings, ConstSettings.LocalLocation, settings.LocalLocation);
        _iniFileHelper.WriteValue(ConstSettings.Settings, ConstSettings.FolderPattern, settings.FolderPattern);
        _iniFileHelper.WriteValue(ConstSettings.Settings, ConstSettings.FileExtensions, settings.FileExtensions);
        _iniFileHelper.WriteValue(ConstSettings.Settings, ConstSettings.SyncInterval, settings.SyncInterval);
        _iniFileHelper.WriteValue(ConstSettings.Settings, ConstSettings.UseFlatStructure, settings.UseFlatStructure.ToString());
        _iniFileHelper.WriteValue(ConstSettings.Settings, ConstSettings.DuplicateHandling, ((int)settings.DuplicateHandling).ToString());
        
        _iniFileHelper.WriteValue(ConstSettings.Settings, ConstSettings.Username, settings.Username);
        // TODO: 추후 암호화 처리
        _iniFileHelper.WriteValue(ConstSettings.Settings, ConstSettings.Password, settings.Password);
    }

    public void SyncSettings()
    {
        LoadSettings();
    }
}