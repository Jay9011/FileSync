using S1FileSync.Models;
using S1FileSync.Services;
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
        private readonly FileSyncIPCServer _ipcServer;
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        #endregion

        private SyncSettings _settings;
        private readonly TimeSpan _settingsCheckInterval = TimeSpan.FromSeconds(3);

#if DEBUG
        private readonly TimeSpan _iconUpdateInterval = TimeSpan.FromSeconds(5);
#endif
        

        public FileSyncWorker(ILogger<FileSyncWorker> logger, ISettingsService settingsService, IFileSync fileSync, FileSyncIPCServer ipcServer)
        {
            #region 의존 주입

            _logger = logger;
            _settingsService = settingsService;
            _fileSync = fileSync;
            _ipcServer = ipcServer;

            #endregion
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await LoadSettings();
                await _ipcServer.StartAsync(stoppingToken);

                using var syncTimer = new PeriodicTimer(_settings.SyncIntervalTimeSpan);
                using var settingsCheckTimer = new PeriodicTimer(_settingsCheckInterval);
                
                _ipcServer.MessageReceived += OnMessageReceived;
                
#if DEBUG
                using var iconUpdateTimer = new PeriodicTimer(_iconUpdateInterval);
#endif

                // 모든 비동기 작업이 완료될 때까지 대기 (각 작업은 PeriodicTimer를 통해 별도의 타이머로 실행)
#if !DEBUG
                await Task.WhenAll(
                    RunFileSyncLoop(syncTimer, _cancellationTokenSource.Token),
                    RunSettingsCheckLoop(settingsCheckTimer, _cancellationTokenSource.Token)
                );
#endif
#if DEBUG
                await Task.WhenAll(
                    RunUpdateTestLoop(iconUpdateTimer, _cancellationTokenSource.Token)
                );
#endif
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

        /// <summary>
        /// IPC를 통해 메시지를 받은 경우
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="message"></param>
        private void OnMessageReceived(object? sender, FileSyncMessage message)
        {
            try
            {
                switch (message.Content?.Type)
                {
                    case FileSyncMessageType.ProgressUpdate:
                        break;
                    case FileSyncMessageType.StatusChange:
                        break;
                    case FileSyncMessageType.Error:
                        break;
                    case FileSyncMessageType.ServiceCommand:
                    {
                        // TODO: 서비스 명령 처리
                    }
                        break;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while processing the received message");
            }
        }

#if DEBUG
        private async Task RunUpdateTestLoop(PeriodicTimer timer, CancellationToken stoppingToken)
        {
            try
            {
                var random = new Random();
                var statusType = Enum.GetValues<TrayIconStatus>();
                int currentIndex = 0;

                while (!stoppingToken.IsCancellationRequested
                       && await timer.WaitForNextTickAsync(stoppingToken))
                {
                    try
                    {
                        var status = statusType[currentIndex];
                        currentIndex = (currentIndex + 1) % statusType.Length;

                        var message = new FileSyncMessage(FileSyncMessageType.StatusChange, status.ToString());

                        await _ipcServer.SendMessageAsync(message, stoppingToken);
                        _logger.LogInformation($"Sent icon stauts message: {status}");

                        var progress = new FileSyncProgress(
                            FileName: $"test_file_{random.Next(1, 100)}.txt",
                            FileSize: random.Next(1024, 1024 * 1024),
                            Progress: random.Next(0, 101),
                            Speed: random.Next(512, 2048),
                            IsCompleted: random.Next(0, 2) == 1
                        );

                        var progressMessage =
                            new FileSyncMessage(FileSyncMessageType.ProgressUpdate, progress: progress);
                        
                        await _ipcServer.SendMessageAsync(progressMessage, stoppingToken);
                        _logger.LogInformation($"Sent progress message: {progress.FileName}");
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error occurred while sending test message");
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while running update test loop");
            }
        }
#endif

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping the service");
            _cancellationTokenSource.Cancel();
            return base.StopAsync(cancellationToken);
        }
    }
}
