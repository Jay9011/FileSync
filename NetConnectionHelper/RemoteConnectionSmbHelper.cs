using System.IO;
using System.Net;
using NetConnectionHelper.Helpers;
using NetConnectionHelper.Interface;
using NetConnectionHelper.Models;

namespace NetConnectionHelper;

public class RemoteConnectionSmbHelper : IRemoteConnectionHelper
{
    public async Task<(bool, string)> ConnectionAsync(string server, string username, string password)
    {
        string uncPath = GetRightPath(server);

        try
        {
            // 먼저 기존 연결을 확인
            if (await CheckExistingConnectionAsync(uncPath))
            {
                return (true, "Connection already exists");
            }
            else
            {
                // 기존 연결이 만료된 경우 연결 끊기 시도
                try
                {
                    WinNet.WNetCancelConnection2(uncPath, 0, true);
                }
                catch (Exception e)
                {
                    // 기존 연결이 없거나 끊는데 실패해도 계속 진행
                }
            }
            
            // 네트워크 자격 증명
            using (new NetworkConnection(uncPath, new NetworkCredential(username, password)))
            {
                // 디렉터리 존재 여부 확인
                bool exists = Directory.Exists(uncPath);

                if (exists)
                {
                    // 간단한 파일 작업 시도(디렉토리 목록 조회)
                    await Task.Run(() => Directory.GetDirectories(uncPath));

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

    private async Task<bool> CheckExistingConnectionAsync(string uncPath)
    {
        try
        {
            // 간단한 파일 작업을 통해 기존 연결 확인
            await Task.Run(() => Directory.GetDirectories(uncPath));
            return true;
        }
        catch (Exception e)
        {
            return false;
        }
    }

    public string GetRightPath(string server)
    {
        return server.StartsWith(@"\\") ? server : $@"\\{server}";
    }
}