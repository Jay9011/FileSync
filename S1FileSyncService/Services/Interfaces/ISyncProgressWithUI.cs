namespace S1FileSyncService.Services.Interfaces;

public interface ISyncProgressWithUI
{
    /// <summary>
    /// 프로그래스 관련 UI 업데이트
    /// </summary>
    /// <param name="fileName">파일명</param>
    /// <param name="fileSize">파일 크기</param>
    /// <param name="progress">진행도</param>
    /// <param name="speed">전송 속도</param>
    void UpdateProgress(string fileName, long fileSize, double progress, double speed);
    void CompleteProgress(string fileName);
}