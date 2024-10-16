using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using FileIOService;
using FileIOService.Services.Interface;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetConnectionService;
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
            await _host.StartAsync();
            
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
            
            base.OnStartup(e);
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
            // Views
            services.AddSingleton<MainWindow>();
            services.AddSingleton<SettingsView>();

            // ViewModels
            services.AddTransient<MainViewModel>();
            services.AddTransient<SettingsViewModel>();

            // Services
            services.AddSingleton<IRemoteConnectionService, RemoteConnectionSMBService>();
            services.AddSingleton<IPopupService, WindowPopupService>();
            services.AddSingleton<IIniFileService>(sp => new IniFileService(Path.Combine(Directory.GetCurrentDirectory(), "settings.ini")));

            // Helpers
            services.AddTransient<IniFileService>();
            
            // dependencies
            services.AddTransient<SettingsViewModel>(sp =>
                new SettingsViewModel(
                    sp.GetRequiredService<IRemoteConnectionService>(),
                    sp.GetRequiredService<IPopupService>(),
                    sp.GetRequiredService<IIniFileService>()
                ));
        }
    }

}
