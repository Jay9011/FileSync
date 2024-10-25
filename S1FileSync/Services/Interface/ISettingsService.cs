using S1FileSync.Models;

namespace S1FileSync.Services.Interface;

public interface ISettingsService
{
    string GetSettingsFilePath();
    SyncSettings LoadSettings();
    void SaveSettings(SyncSettings settings);
    void SyncSettings();
}