using System.ComponentModel;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using S1FileSync.Services.Interface;
using S1FileSync.ViewModels;
using S1FileSync.Views;
using Application = System.Windows.Application;

namespace S1FileSync
{
    public partial class MainWindow : Window
    {
        #region 의존 주입
        
        private readonly IServiceProvider _serviceProvider;
        private readonly ITrayIconService _trayIconService;

        #endregion
        
        public MainWindow(MainViewModel mainViewModel, IServiceProvider serviceProvider, ITrayIconService trayIconService, SettingsView settingsView, FileSyncProgressView progressView)
        {
            InitializeComponent();

            #region 의존 주입

            _serviceProvider = serviceProvider;
            _trayIconService = trayIconService;

            #endregion
            
            DataContext = mainViewModel;
            
            SettingsFrame.Navigate(settingsView);
            ProgressViewContainer.Content = progressView;

            // 트레이 아이콘 서비스
            _trayIconService.WindowOpenRequested += WindowOpenRequested;
            _trayIconService.ShutdownRequested += ShutdownRequested;
            StateChanged += MainWindowStateChanged;
            Closing += MainWindowClosing;
            _trayIconService.Initialize();
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
    }
}