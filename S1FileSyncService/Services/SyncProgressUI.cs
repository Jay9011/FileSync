using S1FileSync.Models;
using S1FileSyncService.Services.Interfaces;

namespace S1FileSyncService.Services;

public class SyncProgressUI : ISyncProgressWithUI
{
    #region 의존 주입

    private readonly ILogger<SyncProgressUI> _logger;
    private readonly FileSyncIPCServer _ipcServer;

    #endregion

    public SyncProgressUI(ILogger<SyncProgressUI> logger, FileSyncIPCServer ipcServer)
    {
        #region 의존 주입

        _logger = logger;
        _ipcServer = ipcServer;

        #endregion
    }

    public void UpdateProgress(string fileName, long fileSize, double progress, double speed)
    {
        try
        {
            var progressData = new FileSyncProgress(
                FileName: fileName,
                FileSize: fileSize,
                Progress: progress,
                Speed: speed,
                IsCompleted: false
            );
            
            var message = new FileSyncMessage(FileSyncMessageType.ProgressUpdate, progress: progressData);
            
            Task.Run(async () => await _ipcServer.SendMessageAsync(message))
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to update progress");
        }
    }

    public void CompleteProgress(string fileName)
    {
        try
        {
            var progressData = new FileSyncProgress(
                FileName: fileName,
                FileSize: 0,
                Progress: 100,
                Speed: 0,
                IsCompleted: true
            );
            
            var message = new FileSyncMessage(FileSyncMessageType.ProgressUpdate, progress: progressData);
            
            Task.Run(async () => await _ipcServer.SendMessageAsync(message))
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to complete progress");
        }
    }
}