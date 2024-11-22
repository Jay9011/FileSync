using System.Collections.ObjectModel;
using System.Windows.Forms;
using System.Windows.Threading;
using S1FileSync.Models;
using S1FileSync.Services;
using S1FileSync.Services.Interface;

namespace S1FileSync.ViewModels;

public class FileSyncProgressViewModel : PropertyChangeNotifier
{
    public ObservableCollection<FileSyncProgressItem> SyncItems { get; }

    private readonly Dispatcher _dispatcher;
    private readonly TimeSpan ResetProgressInterval = new TimeSpan(0, 0, 10, 0);

    private readonly PeriodicTimer _checkTimer;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Task? _itemCheckTask;

    #region 의존 주입

    private readonly ITrayIconService _trayIconService;

    #endregion
 
    public FileSyncProgressViewModel(ITrayIconService trayIconService)
    {
        #region 의존 주입

        _trayIconService = trayIconService;
        
        #endregion
        
        _dispatcher = Dispatcher.CurrentDispatcher;
        SyncItems = new ObservableCollection<FileSyncProgressItem>();
        
        _checkTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        _itemCheckTask = ItemCheck();
    }

    public FileSyncProgressItem AddOrFindItem(string fileName, long fileSize)
    {
        FileSyncProgressItem item = null;

        _dispatcher.Invoke(() =>
        {
            item = SyncItems.FirstOrDefault(i => i.FileName == fileName);

            if (item == null)
            {
                item = new FileSyncProgressItem
                {
                    FileName = fileName,
                    FileSize = fileSize,
                    Progress = 0,
                    IsCompleted = false,
                    LastSyncTime = DateTime.Now
                };
                SyncItems.Add(item);
            }
        });

        return item;
    }

    public void RemoveCompletedItems(bool remainShortlyItem = true)
    {
        _dispatcher.Invoke(() =>
        {
            var itemsToRemove = SyncItems.Where(
                item => item.IsCompleted && 
                        (!remainShortlyItem || (DateTime.Now - item.LastSyncTime) > ResetProgressInterval)
                ).ToList();

            foreach (var item in itemsToRemove)
            {
                SyncItems.Remove(item);
            }

            if (SyncItems.All(item => item.IsCompleted))
            {
                OnAllItemsCompleted();
            }
        });
    }

    /// <summary>
    /// Progress Item을 사용하여 아이템 리스트 업데이트
    /// </summary>
    /// <param name="progress"></param>
    public void UpdateProgress(FileSyncProgress progress)
    {
        var item = AddOrFindItem(progress.FileName, progress.FileSize);
        if (item == null)
        {
            return;
        }
        item.Progress = progress.Progress;
        item.SyncSpeed = progress.Speed;
        item.IsCompleted = progress.IsCompleted;
    }
    
    private void OnAllItemsCompleted()
    {
        if (_trayIconService.GetStatus() == TrayIconStatus.Syncing)
        {
            _trayIconService.SetStatus(TrayIconStatus.Normal);   
        }
    }

    private async Task ItemCheck()
    {
        try
        {
            while (await _checkTimer.WaitForNextTickAsync(_cancellationTokenSource.Token))
            {
                RemoveCompletedItems();
            }
        }
        catch (Exception e)
        {
            // ignored
        }
    }

}