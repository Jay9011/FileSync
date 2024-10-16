using System.IO;
using System.Net;
using NetConnectionService.Helpers;

namespace NetConnectionService;

public class RemoteConnectionSMBService : IRemoteConnectionService
{
    public async Task<(bool, string)> TestConnectionAsync(string server, string username, string password)
    {
        try
        {
            string uncPath = GetUncPath(server);

            // 네트워크 자격 증명
            using (new NetworkConnection(uncPath, new NetworkCredential(username, password)))
            {
                // 디렉터리 존재 여부 확인
                bool exists = Directory.Exists(uncPath);

                if (exists)
                {
                    // 간단한 파일 작업 시도(디렉토리 목록 조회)
                    await Task.Run(() =>
                    {
                        Directory.GetDirectories(uncPath); 
                        
                    });

                    return (exists, "Connection successful");
                }
                else
                {
                    return (false, "Directory not found");
                }
            }
        }
        catch (UnauthorizedAccessException e)
        {
            // 인증 실패
            return (false, e.Message);
        }
        catch (IOException e)
        {
            // 서버 연결 실패
            return (false, e.Message);
        }
        catch (Exception e)
        {
            // 기타 오류
            return (false, e.Message);
        }

        return (false, "Connection failed");
    }

    private string GetUncPath(string server)
    {
        return server.StartsWith(@"\\") ? server : $@"\\{server}";
    }
}