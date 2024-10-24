﻿using System.Net;
using System.Security.Cryptography;
using NetConnectionHelper.Helpers;
using NetConnectionHelper.Interface;
using S1FileSync.Models;
using S1FileSync.Services.Interface;
using S1FileSyncService.Services.Interfaces;

namespace S1FileSyncService.Services;

public class FileSyncService : IFileSync
{
    #region 의존 주입

    private readonly ILogger<FileSyncWorker> _logger;
    private readonly ISettingsService _settingsService;
    private readonly IRemoteConnectionHelper _connectionHelper;

    #endregion
    
    public FileSyncService(ILogger<FileSyncWorker> logger, ISettingsService settingsService, IRemoteConnectionHelper connectionHelper)
    {
        #region 의존 주입
        
        _logger = logger;
        _settingsService = settingsService;
        _connectionHelper = connectionHelper;

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

            await SyncFile(remoteFile.FullName, localFilePath, settings);
        });
        
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 단일 파일 동기화
    /// </summary>
    /// <param name="sourceFilePath">원본 파일 경로</param>
    /// <param name="destFilePath">목적 파일 경로</param>
    /// <param name="settings"></param>
    private async Task SyncFile(string sourceFilePath, string destFilePath, SyncSettings settings)
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
                    shouldCopy = await IsFileChangedAsync(sourceFilePath, destFilePath);
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
            
            await CopyFileAsync(sourceFilePath, destFilePath);
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
    /// <param name="localFilePath">로컬 파일 경로</param>
    /// <param name="remoteFilePath">원격 파일 경로</param>
    /// <returns></returns>
    private async Task<bool> IsFileChangedAsync(string localFilePath, string remoteFilePath)
    {
        using (var md5 = MD5.Create())
        {
            using (var localStream = File.OpenRead(localFilePath))
            using (var remoteStream = File.OpenRead(remoteFilePath))
            {
                var localHash = md5.ComputeHashAsync(localStream);
                var remoteHash = md5.ComputeHashAsync(remoteStream);
                
                await Task.WhenAll(localHash, remoteHash);
                
                return !localHash.Result.SequenceEqual(remoteHash.Result);
            }
        }
    }

    /// <summary>
    /// 파일을 복사한다.
    /// </summary>
    /// <param name="sourceFilePath">원본 파일 경로</param>
    /// <param name="destinationFilePath">대상 파일 경로</param>
    private async Task CopyFileAsync(string sourceFilePath, string destinationFilePath)
    {
        var directory = Path.GetDirectoryName(destinationFilePath);
        if (!Directory.Exists(directory))
        {
            if (directory != null) Directory.CreateDirectory(directory);
        }
        
        using (var sourceStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan))
        using (var destinationStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            await sourceStream.CopyToAsync(destinationStream);
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
}