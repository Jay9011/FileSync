using Microsoft.Extensions.Logging;
using Moq;
using NetConnectionHelper;
using NetConnectionHelper.Interface;
using S1FileSync.Models;
using S1FileSync.Services.Interface;
using S1FileSyncService;
using S1FileSyncService.Services;

namespace FileSyncApp.Tests
{
    public class FileSyncServiceTest : IDisposable
    {
        private readonly string _testLocalPath;
        private readonly string _testRemotePath;
        private readonly Mock<ILogger<FileSyncWorker>> _loggerMock;
        private readonly Mock<ISettingsService> _settingsServiceMock;
        private readonly IRemoteConnectionHelper _remoteConnectionHelper;
        private readonly FileSyncService _fileSyncService;
        
        public FileSyncServiceTest()
        {
            _testLocalPath = Path.Combine(Path.GetTempPath(), "TestLocalFolder");
            _testRemotePath = Path.Combine("\\\\192.168.0.52\\Project\\교육자료\\샘플데이터");
            Directory.CreateDirectory(_testLocalPath);
            
            _loggerMock = new Mock<ILogger<FileSyncWorker>>();
            _settingsServiceMock = new Mock<ISettingsService>();
            _settingsServiceMock.Setup(s => s.LoadSettings()).Returns(new SyncSettings
            {
                LocalLocation = _testLocalPath,
                RemoteLocation = _testRemotePath,
                Username = "admin",
                Password = "secu13579"
            });
            
            _remoteConnectionHelper = new RemoteConnectionSmbHelper();
            _fileSyncService = new FileSyncService(_loggerMock.Object, _settingsServiceMock.Object, _remoteConnectionHelper);
        }
        
        [Fact]
        public async Task SyncFiles_ShouldCopyFileFromRemoteToLocal()
        {
            // Arrange
            string testFileName = "testfile.txt";
            string remoteTestFilePath = Path.Combine(_testRemotePath, testFileName);
            string localTestFilePath = Path.Combine(_testLocalPath, testFileName);
            File.WriteAllText(remoteTestFilePath, "Original Content");
            File.WriteAllText(localTestFilePath, "Old Content");
            
            // Act
            await _fileSyncService.SyncFile();
            
            // Assert
            Assert.Equal("Original Content", File.ReadAllText(localTestFilePath));
        }
        
        [Fact]
        public async Task SyncFiles_ShouldNotUpdateUnchangedFile()
        {
            // Arrange
            string testFileName = "testfile.txt";
            string remoteTestFilePath = Path.Combine(_testRemotePath, testFileName);
            string localTestFilePath = Path.Combine(_testLocalPath, testFileName);
            string content = "Same Content";
            File.WriteAllText(remoteTestFilePath, content);
            File.WriteAllText(localTestFilePath, content);
            DateTime originalLastWriteTime = File.GetLastWriteTime(localTestFilePath);
            
            // Act
            await _fileSyncService.SyncFile();
            
            // Assert
            Assert.Equal(originalLastWriteTime, File.GetLastWriteTime(localTestFilePath));
        }
        
        public void Dispose()
        {
            Directory.Delete(_testLocalPath, true);
        }
    }
}