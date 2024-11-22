using S1FileSync.Services.Interface;
using S1FileSync.ViewModels;

namespace S1FileSync.Services;

public class RemoteServerConnectionChecker : PropertyChangeNotifier, IRemoteServerConnectionChecker
{
    private bool _remoteServerConnected = false;

    public bool RemoteServerConnected
    {
        get => _remoteServerConnected;
        set => SetField(ref _remoteServerConnected, value);
    }
    
    private string _RemoteServerConnectionStatus = "Disconnected";

    public string RemoteServerConnectionStatus
    {
        get => _RemoteServerConnectionStatus;
        set => SetField(ref _RemoteServerConnectionStatus, value);
    }

    public void ConnectionChange(bool isConnected, string message)
    {
        RemoteServerConnected = isConnected;
        RemoteServerConnectionStatus = message;
    }
}