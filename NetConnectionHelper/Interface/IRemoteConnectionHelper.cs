namespace NetConnectionHelper.Interface;

public interface IRemoteConnectionHelper
{
    /// <summary>
    /// 서버에 연결 테스트
    /// </summary>
    /// <param name="server">원격지 주소</param>
    /// <param name="username">사용자 계정</param>
    /// <param name="password">비밀번호</param>
    /// <returns></returns>
    Task<(bool, string)> ConnectionAsync(string server, string username, string password);
    /// <summary>
    /// 올바른 경로로 가져오기
    /// </summary>
    /// <param name="server">사용자 입력 서버 주소</param>
    /// <returns></returns>
    string GetRightPath(string server);
}