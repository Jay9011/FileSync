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

        #endregion

        private Timer _syncTimer;
        private SyncSettings _settings;

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
            _settings = _settingsService.LoadSettings();
            StartSyncTimer();
            
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                }
                await Task.Delay(1000, stoppingToken);
            }
        }

        /// <summary>
        /// 파일 동기화 타이머 시작
        /// </summary>
        private void StartSyncTimer()
        {
            _syncTimer = new Timer(SyncTimerCallback, null, TimeSpan.Zero, _settings.SyncIntervalTimeSpan);
        }

        /// <summary>
        /// 파일 동기화 타이머 콜백
        /// </summary>
        /// <param name="state"></param>
        private async void SyncTimerCallback(object? state)
        {
            try
            {
                await _fileSync.SyncRemoteFile();
                _logger.LogInformation("File sync completed at: {time}", DateTimeOffset.Now);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while syncing files");
            }
        }
    }
}
