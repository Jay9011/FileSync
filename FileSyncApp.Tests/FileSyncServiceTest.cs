using System.Text.RegularExpressions;
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
            _testRemotePath = Path.Combine("\\\\192.168.0.52\\Project\\윤종섭");
            Directory.CreateDirectory(_testLocalPath);
            
            _loggerMock = new Mock<ILogger<FileSyncWorker>>();
            _settingsServiceMock = new Mock<ISettingsService>();
            _settingsServiceMock.Setup(s => s.LoadSettings()).Returns(new SyncSettings
            {
                LocalLocation = _testLocalPath,
                RemoteLocation = _testRemotePath,
                Username = "admin",
                Password = "secu13579",
                FolderPattern = "Person_{{yyMMdd}}",
                FileExtensions = "txt, jpg, png"
            });
            
            _remoteConnectionHelper = new RemoteConnectionSmbHelper();
            _fileSyncService = new FileSyncService(_loggerMock.Object, _settingsServiceMock.Object, _remoteConnectionHelper);
        }
        
        [Fact]
        public void DateFormats_ShouldBeExtracted()
        {
            // Arrange
            var settings = new SyncSettings
            {
                FolderPattern = "{{yy}}_Person_{{MMdd}}_{{HH}}"
            };
            
            // Act
            var dateFormats = settings.GetPatternFormats(settings.FolderPattern).ToList();
            
            // Assert
            Assert.Equal(3, dateFormats.Count);
            Assert.Equal("yy", dateFormats[0]);
            Assert.Equal("MMdd", dateFormats[1]);
            Assert.Equal("HH", dateFormats[2]);
        }
        
        [Theory]
        [InlineData("{{yy}}_Person_{{MMdd}}", 2)]
        [InlineData("Normal_Folder", 0)]
        [InlineData("{{yyyy}}_{{MM}}_{{dd}}_{{HH}}", 4)]
        [InlineData("Log_{{yyyyMMdd}}", 1)]
        public void GetDateGroupCount_ShouldReturnCorrectCount(string folderPattern, int expectedCount)
        {
            // Arrange
            var settings = new SyncSettings
            {
                FolderPattern = folderPattern
            };
            
            // Act & Assert
            Assert.Equal(expectedCount, settings.GetPatternCount(folderPattern));
        }
        
        [Fact]
        public void MultipleDataGroups_ShouldMatch()
        {
            // Arrange
            var settings = new SyncSettings
            {
                FolderPattern = "{{yy}}_Person_{{MMdd}}"
            };
            
            var today = DateTime.Now;
            var expectedFolder = $"{today:yy}_Person_{today:MMdd}";
            
            // Act & Assert
            Assert.Equal(2, settings.GetPatternCount(settings.FolderPattern));
            Assert.True(settings.ShouldSyncFolder(expectedFolder));
            Assert.False(settings.ShouldSyncFolder($"{today.AddDays(1):yy}_Person_{today.AddDays(1):MMdd}"));
        }

        [Fact]
        public void ComplexPattern_ShouldMatch()
        {
            // Arrange
            var settings = new SyncSettings
            {
                FolderPattern = "Data_{{yyyy}}_Q{{MM}}_Day{{dd}}_{{HH}}h"
            };

            var today = DateTime.Now;
            var expectedFolder = $"Data_{today:yyyy}_Q{today:MM}_Day{today:dd}_{today:HH}h";
            var wrongFolder = $"Data_{today:yyyy}_Q{today:MM}_Day{today.AddDays(1):dd}_{today:HH}h";

            // Act & Assert
            Assert.Equal(4, settings.GetPatternCount(settings.FolderPattern));
            Assert.True(settings.ShouldSyncFolder(expectedFolder));
            Assert.False(settings.ShouldSyncFolder(wrongFolder));
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
            await _fileSyncService.SyncRemoteFile();
            
            // Assert
            Assert.Equal("Original Content", File.ReadAllText(localTestFilePath));
        }

        [Fact]
        public async Task SyncFile_WithFlatStructure_ShouldMergeFiles()
        {
            // Arrange
            var settings = new SyncSettings
            {
                LocalLocation = _testLocalPath,
                RemoteLocation = _testRemotePath,
                Username = "admin",
                Password = "secu13579",
                FolderPattern = "",
                FileExtensions = "txt, jpg, png",
                UseFlatStructure = true,
                DuplicateHandling = SyncSettings.DuplicateFileHandling.ReplaceIfDifferent
            };
            
            _settingsServiceMock.Setup(s => s.LoadSettings()).Returns(settings);
            
            // 원격 폴더에 테스트 파일 생성
            var remoteFolder1 = Path.Combine(_testRemotePath, "Person_241020");
            var remoteFolder2 = Path.Combine(_testRemotePath, "Person_241022");
            Directory.CreateDirectory(remoteFolder1);
            Directory.CreateDirectory(remoteFolder2);
            File.WriteAllText(Path.Combine(remoteFolder1, "201011.jpg"), "Old Photo");
            File.WriteAllText(Path.Combine(remoteFolder2, "201011.jpg"), "New Photo");
            
            // Act
            await _fileSyncService.SyncRemoteFile();
            
            // Assert
            var localFilePath = Path.Combine(_testLocalPath, "201011.jpg");
            Assert.True(File.Exists(localFilePath));
            Assert.Equal("New Photo", File.ReadAllText(localFilePath));
        }
        
        public void Dispose()
        {
            // Directory.Delete(_testLocalPath, true);
        }
    }
}