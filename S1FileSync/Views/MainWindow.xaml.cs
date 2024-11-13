using System.ComponentModel;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
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
        
        private MainViewModel _viewModel => (MainViewModel)DataContext;
        private readonly IServiceProvider _serviceProvider;
        private readonly ITrayIconService _trayIconService;

        #endregion

        private bool _isSidebarExpanded = true;
        private bool _isDarkTheme = true;
        
        public MainWindow(MainViewModel mainViewModel, IServiceProvider serviceProvider, ITrayIconService trayIconService, SettingsView settingsView, SyncMonitorView syncMonitorView, FileSyncProgressView progressView, FileSyncIPCClient ipcClient)
        {
            InitializeComponent();

            #region 의존 주입

            _serviceProvider = serviceProvider;
            _trayIconService = trayIconService;

            #endregion

            mainViewModel.MonitorView = syncMonitorView;
            mainViewModel.SettingsView = settingsView;
            
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
            _isSidebarExpanded = !_isSidebarExpanded;
            var storyboard = (Storyboard)FindResource(_isSidebarExpanded ? "SidebarExpandAnimation" : "SidebarCollapseAnimation");
            storyboard.Begin();
        }

        private void ThemeToggleClick(object sender, RoutedEventArgs e)
        {
            _viewModel.IsDarkTheme = !_viewModel.IsDarkTheme;
            ApplyTheme();
        }

        /// <summary>
        /// 테마 적용
        /// </summary>
        private void ApplyTheme()
        {
            var existingTheme = Application.Current.Resources.MergedDictionaries.FirstOrDefault(d => d.Source?.ToString().Contains("Theme") == true);

            if (existingTheme != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(existingTheme);
            }
            
            var themeDictionary = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Views/Themes/{(_viewModel.IsDarkTheme ? "Dark" : "Light")}Theme.xaml")
            };
            
            Application.Current.Resources.MergedDictionaries.Add(themeDictionary);
        }
    }
}