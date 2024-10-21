using System.ComponentModel;
using System.Net;
using NetConnectionHelper.Models;

namespace NetConnectionHelper.Helpers;

public class NetworkConnection : IDisposable
{
    private string _networkName;

    /// <summary>
    /// 네트워크 연결 생성
    /// </summary>
    /// <param name="networkName">네트워크 주소</param>
    /// <param name="credential">인증 정보</param>
    /// <exception cref="Win32Exception">연결 실패 예외</exception>
    public NetworkConnection(string networkName, NetworkCredential credential)
    {
        _networkName = networkName;

        var netResource = new WinNet.NetResource()
        {
            Scope = WinNet.ResourceScope.GlobalNetwork,
            ResourceType = WinNet.ResourceType.Disk,
            DisplayType = WinNet.ResourceDisplayType.Share,
            RemoteName = networkName
        };

        var result = WinNet.WNetAddConnection2(
            netResource,
            credential.Password,
            credential.UserName,
            0);

        if (result != 0)
        {
            throw new Win32Exception(result);
        }
    }
    
    ~NetworkConnection()
    {
        Dispose(false);
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        WinNet.WNetCancelConnection2(_networkName, 0, true);
    }
}