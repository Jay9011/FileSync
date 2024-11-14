namespace S1FileSyncService.Services.Interfaces;

public interface IFileSync
{
    /// <summary>
    /// 파일 동기화
    /// </summary>
    /// <returns></returns>
    public Task SyncRemoteFile();
    
    /// <summary>
    /// 서버 연결 테스트
    /// </summary>
    /// <returns></returns>
    public Task<(bool, string)> TestConnection();
}