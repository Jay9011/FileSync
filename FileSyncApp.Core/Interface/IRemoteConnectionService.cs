namespace FileSyncApp.Core;

public interface IRemoteConnectionService
{
    /// <summary>
    /// 서버에 연결 테스트
    /// </summary>
    /// <param name="server">원격지 주소</param>
    /// <param name="username">사용자 계정</param>
    /// <param name="password">비밀번호</param>
    /// <returns></returns>
   Task<(bool, string)> TestConnectionAsync(string server, string username, string password);
}