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
    public TimeSpan SyncInterval { get; set; }
    
}