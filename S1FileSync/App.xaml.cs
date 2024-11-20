using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using FileIOHelper;
using FileIOHelper.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Toolkit.Uwp.Notifications;
using NetConnectionHelper;
using NetConnectionHelper.Interface;
using S1FileSync.Helpers;
using S1FileSync.Models;
using S1FileSync.Services;
using S1FileSync.Services.Interface;
using S1FileSync.ViewModels;
using S1FileSync.Views;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace S1FileSync
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly IHost _host;
        private static Mutex _mutex;
        private const string MutexName = "S1FileSync";

        public App()
        {
            _host = Host.CreateDefaultBuilder().ConfigureServices((context, services) =>
                {
                    ConfigureServices(services);
                })
                .Build();
        }
        
        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                _mutex = new Mutex(true, MutexName, out bool createdNew);

                if (!createdNew)
                {
                    NativeMethod.MessageBox(IntPtr.Zero, "Another instance of the application is already running.", "S1 File Sync", NativeMethod.MB_OK | NativeMethod.MB_ICONINFORMATION);
                    Shutdown();
                    return;
                }

                await _host.StartAsync();
            
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();

                if (!e.Args.Contains("--autostart", StringComparer.OrdinalIgnoreCase))
                {
                    mainWindow.Show();
                }
            
                base.OnStartup(e);
            }
            catch (Exception exception)
            {
                File.WriteAllText("error_log.txt", exception.ToString());
                MessageBox.Show($"An error occurred: {exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
        
        protected override async void OnExit(ExitEventArgs e)
        {
            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }

            using (_host)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5));
            }

            base.OnExit(e);
        }

        private void ConfigureServices(IServiceCollection services)
        {
            string? exePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (exePath == null)
            {
                exePath = Directory.GetCurrentDirectory();
            }
            string? settingsPath = Path.Combine(exePath, "settings.ini");
            
            // Services
            services.AddSingleton<ITrayIconService, TrayIconService>();
            services.AddSingleton<IServiceControlService, ServiceControlService>();
            services.AddSingleton<IRemoteServerConnectionChecker, RemoteServerConnectionChecker>();
            services.AddSingleton<IRemoteConnectionHelper, RemoteConnectionSmbHelper>();
            services.AddSingleton<IPopupService, WindowPopupService>();
            services.AddSingleton<IIniFileHelper>(sp => new IniFileHelper(settingsPath));
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<FileSyncIPCClient>();

            // ViewModels
            services.AddSingleton<FileSyncProgressViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<MainViewModel>();

            // Views
            services.AddSingleton<FileSyncProgressView>();
            services.AddSingleton<SyncMonitorView>();
            services.AddSingleton<SettingsView>();
            services.AddSingleton<MainWindow>();

            // dependencies
            services.AddTransient<SettingsViewModel>(sp =>
                new SettingsViewModel(
                    sp.GetRequiredService<IRemoteConnectionHelper>(),
                    sp.GetRequiredService<IPopupService>(),
                    sp.GetRequiredService<ISettingsService>()
                ));
        }
    }

}
