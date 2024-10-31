using System.Text;
using S1FileSync.Models;
using S1FileSync.Services.Interface;
using S1FileSyncService.Services;
using S1FileSyncService.Services.Interfaces;

namespace S1FileSyncService
{
    public class FileSyncWorker : BackgroundService
    {
        #region 의존 주입

        private readonly ILogger<FileSyncWorker> _logger;
        private readonly ISettingsService _settingsService;
        private readonly IFileSync _fileSync;
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        #endregion

        private SyncSettings _settings;
        private readonly TimeSpan _settingsCheckInterval = TimeSpan.FromSeconds(3);

        public FileSyncWorker(ILogger<FileSyncWorker> logger, ISettingsService settingsService, IFileSync fileSync)
        {
            #region 의존 주입

            _logger = logger;
            _settingsService = settingsService;
            _fileSync = fileSync;

            #endregion
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await LoadSettings();

                using var syncTimer = new PeriodicTimer(_settings.SyncIntervalTimeSpan);
                using var settingsCheckTimer = new PeriodicTimer(_settingsCheckInterval);
            
                // 모든 비동기 작업이 완료될 때까지 대기 (각 작업은 PeriodicTimer를 통해 별도의 타이머로 실행)
                await Task.WhenAll(
                    RunFileSyncLoop(syncTimer, _cancellationTokenSource.Token),
                    RunSettingsCheckLoop(settingsCheckTimer, _cancellationTokenSource.Token)
                );
            }
            finally
            {
                _cancellationTokenSource.Cancel();
            }
        }
        
        private async Task LoadSettings()
        {
            _settings = _settingsService.LoadSettings();
            _logger.LogInformation($"Settings loaded - File Path: {_settingsService.GetSettingsFilePath()}");
        }

        /// <summary>
        /// 파일 동기화 루프
        /// </summary>
        /// <param name="timer"></param>
        /// <param name="stoppingToken"></param>
        private async Task RunFileSyncLoop(PeriodicTimer timer, CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested
                       && await timer.WaitForNextTickAsync(stoppingToken))
                {
                    try
                    {
                        await _fileSync.SyncRemoteFile();
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error occurred while syncing files");
                    }
                }
            }
            catch (OperationCanceledException e)
            {
                _logger.LogInformation("File sync loop stopped");
            }
        }

        /// <summary>
        /// 설정 검증 루프
        /// </summary>
        /// <param name="timer"></param>
        /// <param name="stoppingToken"></param>
        private async Task RunSettingsCheckLoop(PeriodicTimer timer, CancellationToken stoppingToken)
        {
            try
            {
                const string processPrefix = "Settings check: ";
                string statusMessage = "No changes detected";
                
                while (!stoppingToken.IsCancellationRequested
                       && await timer.WaitForNextTickAsync(stoppingToken))
                {
                    try
                    {
                        var newSettings = _settingsService.LoadSettings();

                        // TODO: 설정 검증
                        
                        
                        _logger.LogInformation($"{processPrefix}{statusMessage} at: {DateTimeOffset.Now}");
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error occurred while checking settings");
                    }
                }
            }
            catch (OperationCanceledException e)
            {
                _logger.LogInformation("Settings check loop stopped");
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping the service");
            _cancellationTokenSource.Cancel();
            return base.StopAsync(cancellationToken);
        }
    }
}
