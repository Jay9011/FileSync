using S1FileSync.Models;
using S1FileSyncService.Services.Interfaces;

namespace S1FileSyncService.Services;

public class SyncSendMessage : ISendMessage
{
    #region 의존 주입

    private readonly FileSyncIPCServer _ipcServer;

    #endregion
    
    public SyncSendMessage(FileSyncIPCServer ipcServer)
    {
        #region 의존 주입

        _ipcServer = ipcServer;

        #endregion
    }

    public async Task SendMessageAsync(FileSyncMessageType messageType, string message, CancellationToken cancellationToken)
    {
        try
        {
            var syncMessage = new FileSyncMessage(messageType, message ??= "");
            await _ipcServer.SendMessageAsync(syncMessage, cancellationToken);
        }
        catch (Exception e)
        {
            throw;
        }
    }

    public async Task SendMessageAsync(FileSyncMessageType messageType, ConnectionStatusType connectionStatusType, string message, CancellationToken cancellationToken)
    {
        try
        {
            var syncMessage = new FileSyncMessage(messageType, connectionStatusType, message ??= "");
            await _ipcServer.SendMessageAsync(syncMessage, cancellationToken);
        }
        catch (Exception e)
        {
            throw;
        }
    }
}