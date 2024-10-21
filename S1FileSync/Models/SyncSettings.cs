using System.Text.RegularExpressions;
using S1FileSync.Helpers;

namespace S1FileSync.Models;

public class SyncSettings
{
    /// <summary>
    /// 원격지 주소
    /// </summary>
    public string RemoteLocation { get; set; }
    /// <summary>
    /// 원격지 로그인 계정
    /// </summary>
    public string Username { get; set; }
    /// <summary>
    /// 원격지 로그인 비밀번호
    /// </summary>
    public string Password { get; set; }
    /// <summary>
    /// 로컬 저장 주소
    /// </summary>
    public string LocalLocation { get; set; }
    /// <summary>
    /// 확장자 저장용
    /// </summary>
    public string FileExtensions { get; set; }

    /// <summary>
    /// 동기화 주기
    /// </summary>
    private string _syncInterval;
    public string SyncInterval
    {
        get => _syncInterval;
        set
        {
            if (TimeSpanHelper.IsValidTimeSpanFormat(value))
            {
                _syncInterval = TimeSpanHelper.ParseTimeSpan(value).ToString();
            }
            else
            {
                throw new ArgumentException("Invalid TimeSpan format. Expected format: [dd.]hh:mm:ss");
            }
        }
    }
    public TimeSpan SyncIntervalTimeSpan => TimeSpanHelper.ParseTimeSpan(SyncInterval);
    
    public SyncSettings()
    {
        RemoteLocation = string.Empty;
        Username = string.Empty;
        Password = string.Empty;
        LocalLocation = string.Empty;
        FileExtensions = string.Empty;
        SyncInterval = ConstSettings.DefaultSyncInterval;
    }
}

public static class ConstSettings
{
    public const string Settings = "Settings";
    public const string RemoteLocation = "RemoteLocation";
    public const string Username = "Username";
    public const string Password = "Password";
    public const string LocalLocation = "LocalLocation";
    public const string FileExtensions = "FileExtensions";
    public const string SyncInterval = "SyncInterval";
    public const string DefaultSyncInterval = "1.00:00:00";
}