using S1FileSync.Services.Interface;

namespace S1FileSync.Services;

public class RemoteServerConnectionChecker : IRemoteServerConnectionChecker
{
    private bool _remoteServerConnected;

    public bool RemoteServerConnected
    {
        get { return _remoteServerConnected; }
        set
        {
            _remoteServerConnected = value;
        }
    }
    
    private string _RemoteServerConnectionStatus = "Disconnected";

    public string RemoteServerConnectionStatus
    {
        get { return _RemoteServerConnectionStatus; }
        set
        {
            _RemoteServerConnectionStatus = value;
        }
    }

    public void ConnectionChange(bool isConnected, string message)
    {
        RemoteServerConnected = isConnected;
        RemoteServerConnectionStatus = message;
    }
}