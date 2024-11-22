using System.ComponentModel;
using System.ServiceProcess;
using System.Windows.Input;
using S1FileSync.Helpers;
using S1FileSync.Services;
using S1FileSync.Services.Interface;
using S1FileSync.Views;

namespace S1FileSync.ViewModels
{
    public class SyncMonitorViewModel : PropertyChangeNotifier
    {
        #region 의존 주입

        public required FileSyncProgressView ProgressView { get; set; }
        
        public IServiceControlService ServiceControlService { get; private set; }
        public FileSyncProgressViewModel ProgressViewModel { get; private set; }
        public IRemoteServerConnectionChecker RemoteServerConnectionChecker { get; private set; }
        public FileSyncIPCClient IpcClient { get; private set; }

        #endregion

        public bool IsConnected
        {
            get => ServiceControlService.Status == ServiceControllerStatus.Running &&
                   RemoteServerConnectionChecker.RemoteServerConnected;
        }

        public ICommand StartSyncCommand { get; set; }
        public ICommand StopSyncCommand { get; set; }
        public ICommand ClearItemList { get; set; }

        public SyncMonitorViewModel(IServiceControlService serviceControlService, FileSyncProgressView progressView, FileSyncProgressViewModel progressViewModel, IRemoteServerConnectionChecker remoteServerConnectionChecker, FileSyncIPCClient ipcClient)
        {
            #region 의존 주입

            ServiceControlService = serviceControlService;
            ProgressView = progressView;
            ProgressViewModel = progressViewModel;
            RemoteServerConnectionChecker = remoteServerConnectionChecker;
            IpcClient = ipcClient;

            #endregion
            
            StartSyncCommand = new RelayCommand(async () => await StartSync(),
                canExecute: () => (ServiceControlService.Status == ServiceControllerStatus.Stopped));
            StopSyncCommand = new RelayCommand(async () => await StopSync(),
                canExecute: () => (serviceControlService.Status == ServiceControllerStatus.Running));
            
            ClearItemList = new RelayCommand(async () => await ItemListClear());
            
            ConnectionStatusChangedListener();
        }
        
        /// <summary>
        /// 동기화 시작시 실행되는 이벤트 메서드
        /// </summary>
        private async Task StartSync()
        {
            try
            {
                await ServiceControlService.StartServiceAsync();
            }
            catch (Exception e)
            {
                ServiceControlService.StatusMessage = $"Error: {e.Message}";
            }
        }

        /// <summary>
        /// 동기화 종료시 실행되는 이벤트 메서드
        /// </summary>
        private async Task StopSync()
        {
            try
            {
                await ServiceControlService.StopServiceAsync();
            }
            catch (Exception e)
            {
                ServiceControlService.StatusMessage = $"Error: {e.Message}";
            }
        }
        
        /// <summary>
        /// 동기화 화면의 ItemList를 초기화하는 이벤트 메서드
        /// </summary>
        /// <returns></returns>
        private async Task ItemListClear()
        {
            ProgressViewModel.RemoveCompletedItems(false);
        }

        private void ConnectionStatusChangedListener()
        {
            if (ServiceControlService is INotifyPropertyChanged serviceNotifier)
            {
                serviceNotifier.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(ServiceControlService.Status))
                    {
                        OnPropertyChanged(nameof(IsConnected));
                    }
                };
            }

            if (RemoteServerConnectionChecker is INotifyPropertyChanged remoteServerConnectionCheckerNotifier)
            {
                remoteServerConnectionCheckerNotifier.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(RemoteServerConnectionChecker.RemoteServerConnected))
                    {
                        OnPropertyChanged(nameof(IsConnected));
                    }
                };
            }
        }
    }
}
