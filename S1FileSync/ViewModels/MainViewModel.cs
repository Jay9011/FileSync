using System.Security.Cryptography;
using System.Windows.Input;
using S1FileSync.Helpers;

namespace S1FileSync.ViewModels;

public class MainViewModel : ViewModelBase
{
    private string _status;

    public string Status
    {
        get => _status;
        set
        {
            _status = value;
            OnPropertyChanged();
        }
    }

    public ICommand StartSyncCommand { get; set; }
    public ICommand StopSyncCommand { get; set; }

    public MainViewModel()
    {
        StartSyncCommand = new RelayCommand(StartSync);
        StopSyncCommand = new RelayCommand(StopSync);
    }

    /// <summary>
    /// 동기화 시작시 실행되는 이벤트 메서드
    /// </summary>
    private void StartSync()
    {
        Status = "Synchronization started";
    }

    /// <summary>
    /// 동기화 종료시 실행되는 이벤트 메서드
    /// </summary>
    private void StopSync()
    {
        Status = "Synchronization stopped";
    }
}