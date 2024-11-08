using S1FileSync.Models;

namespace S1FileSync.Services.Interface;

public interface ISettingsService
{
    /// <summary>
    /// 설정 파일 경로 반환
    /// </summary>
    /// <returns></returns>
    string GetSettingsFilePath();
    /// <summary>
    /// 설정 불러오기
    /// </summary>
    /// <returns></returns>
    SyncSettings LoadSettings();
    /// <summary>
    /// 설정 저장
    /// </summary>
    /// <param name="settings"></param>
    void SaveSettings(SyncSettings settings);
    /// <summary>
    /// 설정 동기화
    /// </summary>
    void SyncSettings();
}