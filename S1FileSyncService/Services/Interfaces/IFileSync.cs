namespace S1FileSyncService.Services.Interfaces;

public interface IFileSync
{
    /// <summary>
    /// 파일 동기화
    /// </summary>
    /// <returns></returns>
    public Task SyncRemoteFile();
}