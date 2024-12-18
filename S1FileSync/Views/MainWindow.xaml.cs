﻿using System.ComponentModel;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using S1FileSync.Helpers;
using S1FileSync.Models;
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
        private readonly ITrayIconService _trayIconService;

        #endregion
        
        private const double MinWidthForExpandedSidebar = 800;
        
        private Storyboard? _expeandSidebarStoryboard;
        private Storyboard? _collapseSidebarStoryboard;
        
        public MainWindow(MainViewModel mainViewModel, ITrayIconService trayIconService, SettingsView settingsView, SyncMonitorView syncMonitorView, FileSyncIPCClient ipcClient)
        {
            bool isAutoStart = Environment.GetCommandLineArgs().Contains("--autostart", StringComparer.OrdinalIgnoreCase);
            
            InitializeComponent();

            #region 의존 주입

            _viewModel = mainViewModel;
            _trayIconService = trayIconService;

            #endregion

            mainViewModel.MonitorView = syncMonitorView;
            mainViewModel.SettingsView = settingsView;
            
            _expeandSidebarStoryboard = FindResource("SidebarExpandAnimation") as Storyboard;
            _collapseSidebarStoryboard = FindResource("SidebarCollapseAnimation") as Storyboard;
            
            DataContext = mainViewModel;
            
            // 트레이 아이콘 서비스
            _trayIconService.WindowOpenRequested += WindowOpenRequested;
            _trayIconService.ShutdownRequested += ShutdownRequested;
            _trayIconService.Initialize();

            // 윈도우 상태 변경 감지
            StateChanged += MainWindowStateChanged;
            SizeChanged += OnWindowSizeChanged;
            Closing += MainWindowClosing;

            _ = ipcClient.StartAsync();

            if (isAutoStart)
            {
                Hide();
            }
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

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width < MinWidthForExpandedSidebar &&
                _viewModel.IsSidebarExpanded)
            {
                ToggleSidebarClick(this, new RoutedEventArgs());
            }
            else if (e.NewSize.Width >= MinWidthForExpandedSidebar &&
                     !_viewModel.IsSidebarExpanded)
            {
                ToggleSidebarClick(this, new RoutedEventArgs());
            }
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