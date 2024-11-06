using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using NetConnectionHelper.Helpers;
using NetConnectionHelper.Interface;
using S1FileSync.Models;
using S1FileSync.Services.Interface;
using S1FileSync.ViewModels;
using S1FileSyncService.Services.Interfaces;

namespace S1FileSyncService.Services;

public class FileSyncService : IFileSync
{
    #region 의존 주입

    private readonly ILogger<FileSyncWorker> _logger;
    private readonly ISettingsService _settingsService;
    private readonly IRemoteConnectionHelper _connectionHelper;
    private readonly FileSyncProgressViewModel _progressViewModel;

    #endregion
    
    private const int LargeFileSize = 50 * 1024 * 1024;
    private const float BufferSizeFactor = 0.5f;
    private const int MinBufferSize = 32 * 1024;
    private const int MaxBufferSize = 256 * 1024;
    private const int UIUpdateInterval = 100;
    
    public FileSyncService(ILogger<FileSyncWorker> logger, ISettingsService settingsService, IRemoteConnectionHelper connectionHelper, FileSyncProgressViewModel progressViewModel)
    {
        #region 의존 주입
        
        _logger = logger;
        _settingsService = settingsService;
        _connectionHelper = connectionHelper;
        _progressViewModel = progressViewModel;

        #endregion
    }

    public async Task SyncRemoteFile()
    {
        var settings = _settingsService.LoadSettings();
        string remoteUncPath = _connectionHelper.GetRightPath(settings.RemoteLocation);

        try
        {
            var (isConnected, message) = await _connectionHelper.ConnectionAsync(remoteUncPath, settings.Username, settings.Password);
            
            if (!isConnected)
            {
                _logger.LogError(message);
                return;
            }
            
            await SyncDirectory(settings.LocalLocation, remoteUncPath);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "파일 동기화 중 오류가 발생했습니다.");
        }
    }

    /// <summary>
    /// 디렉토리 내 파일 동기화
    /// </summary>
    /// <param name="localPath">대상 경로</param>
    /// <param name="remotePath">원격 경로</param>
    private async Task SyncDirectory(string localPath, string remotePath)
    {
        var settings = _settingsService.LoadSettings();
        
        // 원격 디렉토리의 모든 파일 가져오기
        var remoteFiles = GetFilteredFiles(remotePath, settings);
        
        // 파일 비교 및 동기화
        var tasks = remoteFiles.Select(async remoteFile =>
        {
            string relativeFilePath = GetRelativePath(remotePath, remoteFile.FullName);
            string localFilePath;

            if (settings.UseFlatStructure)
            {
                localFilePath = Path.Combine(localPath, remoteFile.Name);
            }
            else
            {
                localFilePath = Path.Combine(localPath, relativeFilePath);
            }

            await SyncFile(remoteFile, localFilePath, settings);
        });
        
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 단일 파일 동기화
    /// </summary>
    /// <param name="sourceFilePath">원본 파일 경로</param>
    /// <param name="destFilePath">목적 파일 경로</param>
    /// <param name="settings"></param>
    private async Task SyncFile(FileInfo remoteFileInfo, string destFilePath, SyncSettings settings)
    {
        bool shouldCopy = true;

        if (File.Exists(destFilePath))
        {
            switch (settings.DuplicateHandling)
            {
                case SyncSettings.DuplicateFileHandling.Skip:
                    shouldCopy = false;
                    break;
                case SyncSettings.DuplicateFileHandling.ReplaceIfDifferent:
                    shouldCopy = await IsFileChangedAsync(remoteFileInfo, destFilePath);
                    break;
                case SyncSettings.DuplicateFileHandling.KeepBoth:
                    destFilePath = GetUniqueFileName(destFilePath);
                    break;
                case SyncSettings.DuplicateFileHandling.Replace:
                default:
                    break;
            }
        }

        if (shouldCopy)
        {
            var directory = Path.GetDirectoryName(destFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            await CopyFileAsync(remoteFileInfo, destFilePath);
        }
    }

    /// <summary>
    /// 중복 파일명 처리
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    private string GetUniqueFileName(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return filePath;
        }
        
        string directory = Path.GetDirectoryName(filePath) ?? "";
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        string extension = Path.GetExtension(filePath);
        int count = 1;
        
        string newFilePath;
        do
        {
            newFilePath = Path.Combine(directory, $"{fileName} ({count}){extension}");
            count++;
        } while (File.Exists(newFilePath));
        
        return newFilePath;
    }

    /// <summary>
    /// 필터링 된 파일 가져오기
    /// </summary>
    /// <param name="path">경로</param>
    /// <param name="settings">설정</param>
    private IEnumerable<FileInfo> GetFilteredFiles(string path, SyncSettings settings)
    {
        var directory = new DirectoryInfo(path);
        var allFiles = new List<FileInfo>();

        foreach (var dir in directory.GetDirectories("*", SearchOption.TopDirectoryOnly))
        {
            if (settings.ShouldSyncFolder(dir.Name))
            {
                allFiles.AddRange(GetFilteredFiles(dir.FullName, settings));
            }
        }
        
        allFiles.AddRange(directory.GetFiles().Where(f => settings.ShouldSyncFile(f.Extension)));
        
        return allFiles;
    }

    /// <summary>
    /// 파일 변경 여부 확인
    /// </summary>
    /// <param name="remoteFileInfo">원격 파일 정보</param>
    /// <param name="localFilePath">로컬 파일 경로</param>
    /// <returns></returns>
    private async Task<bool> IsFileChangedAsync(FileInfo remoteFileInfo, string localFilePath)
    {
        var localFileInfo = new FileInfo(localFilePath);

        if (remoteFileInfo.Length != localFileInfo.Length)
        {
            return true;
        }
        
        TimeSpan timeDifference = remoteFileInfo.LastWriteTimeUtc - localFileInfo.LastWriteTimeUtc;
        if (Math.Abs(timeDifference.TotalSeconds) >= 1)
        {
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// 파일을 복사한다.
    /// </summary>
    /// <param name="sourceFilePath">원본 파일 경로</param>
    /// <param name="destinationFilePath">대상 파일 경로</param>
    private async Task CopyFileAsync(FileInfo sourceFileInfo, string destinationFilePath)
    {
        var directory = Path.GetDirectoryName(destinationFilePath);
        if (!Directory.Exists(directory))
        {
            if (directory != null) Directory.CreateDirectory(directory);
        }
        
        var progressItem = _progressViewModel.AddOrUpdateItem(
            Path.GetFileName(sourceFileInfo.Name),
            sourceFileInfo.Length);
        
        int bufferSize = DetermineBufferSize(sourceFileInfo.Length);
        byte[] buffer = new byte[bufferSize];
        
        long totalByteRead = 0;
        var sw = Stopwatch.StartNew();
        
        try
        {
            using (var sourceStream = new FileStream(sourceFileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
            using (var destinationStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                int bytesRead;
                DateTime lastUpdate = DateTime.Now;

                while ((bytesRead = await sourceStream.ReadAsync(buffer)) > 0)
                {
                    await destinationStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalByteRead += bytesRead;

                    var now = DateTime.Now;
                    if ((now - lastUpdate).TotalMilliseconds >= UIUpdateInterval)
                    {
                        double progress = (double)totalByteRead / sourceFileInfo.Length * 100;
                        double speed = totalByteRead / sw.Elapsed.TotalSeconds;
                        
                        progressItem.Progress = progress;
                        progressItem.SyncSpeed = speed;
                        lastUpdate = now;
                    }
                    
                }
            }

            File.SetLastWriteTimeUtc(destinationFilePath, sourceFileInfo.LastWriteTimeUtc);

            progressItem.Progress = 100;
            progressItem.SyncSpeed = 0;
            progressItem.IsCompleted = true;
            progressItem.LastSyncTime = DateTime.Now;
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, $"파일 동기화 중 오류가 발생했습니다: {destinationFilePath}" );
        }
    }

    /// <summary>
    /// 상대 경로를 반환한다.
    /// </summary>
    /// <param name="basePath">기본 경로</param>
    /// <param name="fullPath">전체 경로</param>
    /// <returns></returns>
    private string GetRelativePath(string basePath, string fullPath)
    {
        return fullPath.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar);
    }
    
    /// <summary>
    /// 파일 크기에 따른 버퍼 크기 결정
    /// </summary>
    /// <param name="fileSize">파일 크기</param>
    /// <returns>버퍼 크기</returns>
    /// <exception cref="NotImplementedException"></exception>
    private int DetermineBufferSize(long fileSize)
    {
        if (fileSize <= MinBufferSize)
        {
            return MinBufferSize;
        }

        if (fileSize >= LargeFileSize)
        {
            return MaxBufferSize;
        }
        
        int calculatedSize = (int)(fileSize * BufferSizeFactor);
        return Math.Clamp(calculatedSize, MinBufferSize, MaxBufferSize);
    }

}