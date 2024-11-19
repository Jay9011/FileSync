using System.Collections.ObjectModel;
using System.Windows.Forms;
using System.Windows.Threading;
using S1FileSync.Models;

namespace S1FileSync.ViewModels;

public class FileSyncProgressViewModel : ViewModelBase
{
    private readonly Dispatcher _dispatcher;
    private const int ResetProgressInterval = 24;
    
    private readonly PeriodicTimer _checkTimer;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Task? _itemCheckTask;
    
    public ObservableCollection<FileSyncProgressItem> SyncItems { get; }

    public FileSyncProgressViewModel()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        SyncItems = new ObservableCollection<FileSyncProgressItem>();
        
        _checkTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        _itemCheckTask = ItemCheck();
    }

    public FileSyncProgressItem AddOrUpdateItem(string fileName, long fileSize)
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

    public void RemoveCompletedItems()
    {
        _dispatcher.Invoke(() =>
        {
            var itemsToRemove = SyncItems.Where(item =>
                item.IsCompleted
                && DateTime.Now.Subtract(item.LastSyncTime).TotalHours >= ResetProgressInterval).ToList();

            foreach (var item in itemsToRemove)
            {
                SyncItems.Remove(item);
            }
        });
    }

    /// <summary>
    /// Progress Item을 사용하여 아이템 리스트 업데이트
    /// </summary>
    /// <param name="progress"></param>
    public void UpdateProgress(FileSyncProgress progress)
    {
        var item = AddOrUpdateItem(progress.FileName, progress.FileSize);
        if (item == null)
        {
            return;
        }
        item.Progress = progress.Progress;
        item.SyncSpeed = progress.Speed;
        item.IsCompleted = progress.IsCompleted;
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