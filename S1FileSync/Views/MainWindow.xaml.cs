using System.ComponentModel;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using S1FileSync.Helpers;
using S1FileSync.Services;
using S1FileSync.Services.Interface;
using S1FileSync.ViewModels;
using S1FileSync.Views;
using Application = System.Windows.Application;

namespace S1FileSync
{
    public partial class MainWindow : Window
    {
        #region 의존 주입

        private readonly MainViewModel _viewModel;
        private readonly IServiceProvider _serviceProvider;
        private readonly ITrayIconService _trayIconService;

        #endregion
        
        private Storyboard? _expeandSidebarStoryboard;
        private Storyboard? _collapseSidebarStoryboard;
        
        public MainWindow(MainViewModel mainViewModel, IServiceProvider serviceProvider, ITrayIconService trayIconService, SettingsView settingsView, SyncMonitorView syncMonitorView, FileSyncProgressView progressView, FileSyncIPCClient ipcClient)
        {
            InitializeComponent();

            #region 의존 주입

            _viewModel = mainViewModel;
            _serviceProvider = serviceProvider;
            _trayIconService = trayIconService;

            #endregion

            mainViewModel.MonitorView = syncMonitorView;
            mainViewModel.ProgressView = progressView;
            mainViewModel.SettingsView = settingsView;
            
            _expeandSidebarStoryboard = FindResource("SidebarExpandAnimation") as Storyboard;
            _collapseSidebarStoryboard = FindResource("SidebarCollapseAnimation") as Storyboard;
            
            DataContext = mainViewModel;
            
            // 트레이 아이콘 서비스
            _trayIconService.WindowOpenRequested += WindowOpenRequested;
            _trayIconService.ShutdownRequested += ShutdownRequested;
            StateChanged += MainWindowStateChanged;
            Closing += MainWindowClosing;
            _trayIconService.Initialize();
            
            _ = ipcClient.StartAsync();
        }

        private void WindowOpenRequested(object? sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem)
            {
                // TODO
            }
            else if (sender is NotifyIcon)
            {
                // TODO
            }
            
            Show();
            WindowState = WindowState.Normal;
        }

        private void ShutdownRequested(object? sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MainWindowStateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }
        }

        private void MainWindowClosing(object? sender, CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ToggleSidebarClick(object sender, RoutedEventArgs e)
        {
            if (_viewModel.IsSidebarExpanded)
            {
                _collapseSidebarStoryboard?.Begin();
            }
            else
            {
                _expeandSidebarStoryboard?.Begin();
            }
            
            _viewModel.IsSidebarExpanded = !_viewModel.IsSidebarExpanded;
            /* NavText.Opacity = _viewModel.IsSidebarExpanded ? 1 : 0;*/
        }

        private void ThemeToggleClick(object sender, RoutedEventArgs e)
        {
            ApplyTheme();
        }

        /// <summary>
        /// 테마 적용
        /// </summary>
        private void ApplyTheme()
        {
            ThemeManager.ChangeTheme(_viewModel.IsDarkTheme ? Theme.Dark : Theme.Light);
        }
    }
}