using S1FileSync.Models;

namespace S1FileSync.Services.Interface;

public interface ISettingsService
{
    SyncSettings LoadSettings();
    void SaveSettings(SyncSettings settings);
    void SyncSettings();
}