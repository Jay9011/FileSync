using NetConnectionHelper.Interface;
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
        private readonly ISendMessage _sendMessage;
        private readonly FileSyncIPCServer _ipcServer;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly CancellationTokenSource _workerCts = new();

        #endregion

        private SyncSettings _settings;
        private readonly TimeSpan _connectionCheckInterval = TimeSpan.FromSeconds(3);
        private readonly TimeSpan _settingsCheckInterval = TimeSpan.FromSeconds(3);
        private TimeSpan _currentSyncInterval;

#if DEBUG
        private readonly TimeSpan _iconUpdateInterval = TimeSpan.FromSeconds(3);
#endif
        

        public FileSyncWorker(ILogger<FileSyncWorker> logger, ISettingsService settingsService, IFileSync fileSync, FileSyncIPCServer ipcServer, ISendMessage sendMessage)
        {
            #region 의존 주입

            _logger = logger;
            _settingsService = settingsService;
            _fileSync = fileSync;
            _ipcServer = ipcServer;
            _sendMessage = sendMessage;

            #endregion
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                LoadSettings();
                await _ipcServer.StartAsync(stoppingToken);

                _ipcServer.MessageReceived += OnMessageReceived;

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _workerCts.Token);
#if false
                using var iconUpdateTimer = new PeriodicTimer(_iconUpdateInterval);
#endif
#if true
                // 모든 비동기 작업이 완료될 때까지 대기 (각 작업은 PeriodicTimer를 통해 별도의 타이머로 실행)
                while (!linkedCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        _logger.LogInformation("Starting sync loop with interval : {interval}", _currentSyncInterval);

                        using var loopCts = CancellationTokenSource.CreateLinkedTokenSource(linkedCts.Token);
                        using var syncTimer = new PeriodicTimer(_currentSyncInterval);
                        using var connectionCheckTimer = new PeriodicTimer(_connectionCheckInterval);
                        using var settingsCheckTimer = new PeriodicTimer(_settingsCheckInterval);

                        await Task.WhenAll(
                            RunFileSyncLoop(syncTimer, loopCts.Token),
                            RunConnectionCheckLoop(connectionCheckTimer, loopCts.Token),
                            SettingsCheckTask(settingsCheckTimer, loopCts, loopCts.Token)
                        );
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Settings have changed. restarting sync loop.");
                        continue;
                    }
                    catch (Exception e)
                    {
                        break;
                    }
                }
#endif
#if false
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
        
        private void LoadSettings()
        {
            _settings = _settingsService.LoadSettings();
            _currentSyncInterval = _settings.SyncIntervalTimeSpan;
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
                do
                {
                    try
                    {
                        await _fileSync.SyncRemoteFile();
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error occurred while syncing files");
                    }
                } while (!stoppingToken.IsCancellationRequested
                         && await timer.WaitForNextTickAsync(stoppingToken));
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
        private async Task RunConnectionCheckLoop(PeriodicTimer timer, CancellationToken stoppingToken)
        {
            try
            {
                const string processPrefix = "Connection check: ";
                
                while (!stoppingToken.IsCancellationRequested
                       && await timer.WaitForNextTickAsync(stoppingToken))
                {
                    try
                    {
                        var (isConnected, message) = await _fileSync.TestConnection();
                        var connectionStatus = isConnected ? "Connected" : message;

                        _logger.LogInformation($"{processPrefix}{connectionStatus} at: {DateTimeOffset.Now}");
                        
                        await _sendMessage.SendMessageAsync(FileSyncMessageType.ConnectionStatus, 
                            isConnected ? ConnectionStatusType.Connected : ConnectionStatusType.Disconnected, 
                            message, stoppingToken);
                    }
                    catch (Exception e) when(e is not SettingsChangedException)
                    {
                        _logger.LogError(e, "Error occurred while checking settings");
                    }
                }
            }
            catch (OperationCanceledException e) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Settings check loop stopped");
            }
        }

        private async Task SettingsCheckTask(PeriodicTimer timer, CancellationTokenSource ctSource, CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested
                       && await timer.WaitForNextTickAsync(stoppingToken))
                {
                    var settings = _settingsService.LoadSettings();
                    if (!settings.Equals(_settings))
                    {
                        _logger.LogInformation("Settings changed. Reloading...");

                        _settings = settings;
                        _currentSyncInterval = _settings.SyncIntervalTimeSpan;
                        ctSource.Cancel();
                    }
                }
            }
            catch (OperationCanceledException e) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Settings check loop stopped");
            }
            finally
            {
                timer.Dispose();
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
            _workerCts.Cancel();
            return base.StopAsync(cancellationToken);
        }
    }

    public class SettingsChangedException : Exception
    {
    }
}
