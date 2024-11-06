using System.Collections.ObjectModel;
using System.Windows.Threading;
using S1FileSync.Models;

namespace S1FileSync.ViewModels;

public class FileSyncProgressViewModel : ViewModelBase
{
    private readonly Dispatcher _dispatcher;
    private const int ResetProgressInterval = 24;
    
    public ObservableCollection<FileSyncProgressItem> SyncItems { get; }

    public FileSyncProgressViewModel()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        SyncItems = new ObservableCollection<FileSyncProgressItem>();
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
                    IsCompleted = false
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
}