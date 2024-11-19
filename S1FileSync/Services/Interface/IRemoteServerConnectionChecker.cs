namespace S1FileSync.Services.Interface;

public interface IRemoteServerConnectionChecker
{
    /// <summary>
    /// 원격 서버 연결 상태
    /// </summary>
    public bool RemoteServerConnected { get; set; }
    /// <summary>
    /// 원격 서버 연결 상태 메시지
    /// </summary>
    public string RemoteServerConnectionStatus { get; set; }
    /// <summary>
    /// 원격 서버 연결 상태 변경
    /// </summary>
    /// <param name="isConnected">연결 상태</param>
    /// <param name="message">연결 상태 메시지</param>
    public void ConnectionChange(bool isConnected, string message);
}