namespace S1FileSync.Services.Interface;

public interface ITrayIconService
{
    /// <summary>
    /// 창 열기 요청 이벤트
    /// </summary>
    event EventHandler? WindowOpenRequested;
    /// <summary>
    /// 종료 요청 이벤트
    /// </summary>
    event EventHandler? ShutdownRequested;
    
    /// <summary>
    /// 트레이 아이콘 초기화
    /// </summary>
    void Initialize();
    /// <summary>
    /// 트레이 아이콘 상태 설정
    /// </summary>
    /// <param name="status"></param>
    void SetStatus(TrayIconStatus status);
}