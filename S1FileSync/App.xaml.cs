using System.IO;
using System.Windows;
using FileIOHelper;
using FileIOHelper.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetConnectionHelper;
using NetConnectionHelper.Interface;
using S1FileSync.Services;
using S1FileSync.Services.Interface;
using S1FileSync.ViewModels;
using S1FileSync.Views;

namespace S1FileSync
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly IHost _host;

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
                await _host.StartAsync();
            
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
            
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
            using (_host)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5));
            }
            
            base.OnExit(e);
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Services
            services.AddSingleton<ITrayIconService, TrayIconService>();
            services.AddSingleton<IServiceControlService, ServiceControlService>();
            services.AddSingleton<IRemoteConnectionHelper, RemoteConnectionSmbHelper>();
            services.AddSingleton<IPopupService, WindowPopupService>();
            services.AddSingleton<IIniFileHelper>(sp => new IniFileHelper(Path.Combine(Directory.GetCurrentDirectory(), "settings.ini")));
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<FileSyncIPCClient>();

            // ViewModels
            services.AddSingleton<FileSyncProgressViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<MainViewModel>();

            // Views
            services.AddSingleton<FileSyncProgressView>();
            services.AddSingleton<SettingsView>();
            services.AddSingleton<MainWindow>();

            // Helpers
            services.AddTransient<IniFileHelper>();
            
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
