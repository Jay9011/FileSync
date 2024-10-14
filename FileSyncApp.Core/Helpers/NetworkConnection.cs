using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;

namespace FileSyncApp.Core.Helpers;

public class NetworkConnection : IDisposable
{
    private string _networkName;

    public NetworkConnection(string networkName, NetworkCredential credential)
    {
        _networkName = networkName;

        var netResource = new NativeMethods.NetResource()
        {
            Scope = NativeMethods.ResourceScope.GlobalNetwork,
            ResourceType = NativeMethods.ResourceType.Disk,
            DisplayType = NativeMethods.ResourceDisplaytype.Share,
            RemoteName = networkName
        };
        
        var result = NativeMethods.WNetAddConnection2(
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
        NativeMethods.WNetCancelConnection2(_networkName, 0, true);
    }
}