namespace S1FileSyncService.Services.Interfaces;

public interface ISyncProgressWithUI
{
    void StartProgress(string fileName, long fileSize);
    /// <summary>
    /// 프로그래스 관련 UI 업데이트
    /// </summary>
    /// <param name="fileName">파일명</param>
    /// <param name="fileSize">파일 크기</param>
    /// <param name="progress">진행도</param>
    /// <param name="speed">전송 속도</param>
    void UpdateProgress(string fileName, long fileSize, double progress, double speed);
    /// <summary>
    /// 프로그래스 완료
    /// </summary>
    /// <param name="fileName">파일명</param>
    /// <param name="fileSize">파일 크기(UpdateProgress가 진행되기 전 완료가 동시에 진행되는 경우 사용)</param>
    void CompleteProgress(string fileName, long fileSize);
}