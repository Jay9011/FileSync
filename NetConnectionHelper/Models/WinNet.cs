using System.Runtime.InteropServices;

namespace NetConnectionHelper.Models;

public class WinNet
{
    /// <summary>
    /// https://learn.microsoft.com/ko-kr/windows/win32/api/winnetwk/ns-winnetwk-netresourcea 참고
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public class NetResource
    {
        public ResourceScope Scope;
        public ResourceType ResourceType;
        public ResourceDisplayType DisplayType;
        public int Usage;
        public string LocalName;
        public string RemoteName;
        public string Comment;
        public string Provider;
    }

    public enum ResourceScope
    {
        Connected     = 0x01,
        GlobalNetwork = 0x02,
        Remembered    = 0x03,
        Recent        = 0x04,
        Context       = 0x05,
    }

    public enum ResourceType
    {
        Any      = 0x00,
        Disk     = 0x01,
        Print    = 0x02,
        Reserved = 0x08,
    }

    public enum ResourceDisplayType
    {
        Generic      = 0x00,
        Domain       = 0x01,
        Server       = 0x02,
        Share        = 0x03,
        File         = 0x04,
        Group        = 0x05,
        Network      = 0x06,
        Root         = 0x07,
        ShareAdmin   = 0x08,
        Directory    = 0x09,
        Tree         = 0x0a,
        NdsContainer = 0x0b
    }
    
    [DllImport("mpr.dll")]
    public static extern int WNetAddConnection2(NetResource netResource, string password, string username, int flags);
    
    [DllImport("mpr.dll")]
    public static extern int WNetCancelConnection2(string name, int flags, bool force);
}