using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using S1FileSync.Helpers;

namespace S1FileSync.Models;

public class SyncSettings
{
    /// <summary>
    /// 패턴 정규식
    /// </summary>
    private static readonly Regex PatternRegex = new(@"\{\{(.+?)\}\}");
    
    /// <summary>
    /// 원격지 주소
    /// </summary>
    public string RemoteLocation { get; set; }
    /// <summary>
    /// 원격지 로그인 계정
    /// </summary>
    public string Username { get; set; }
    /// <summary>
    /// 원격지 로그인 비밀번호
    /// </summary>
    public string Password { get; set; }
    /// <summary>
    /// 로컬 저장 주소
    /// </summary>
    public string LocalLocation { get; set; }

    #region 폴더 패턴 (FolderPattern)

    /// <summary>
    /// 폴더 패턴 저장용
    /// </summary>
    private string _folderPattern;
    public string FolderPattern
    {
        get => _folderPattern;
        set
        {
            if (value?.Contains('\\') == true)
            {
                value = value.Replace("\\{", "{").Replace("\\}", "}");
            }
        
            _folderPattern = value;
            ValidateFolderPattern();
        }
    }
    
    #endregion

    #region 확장자 (FileExtensions)

    /// <summary>
    /// 확장자 저장용
    /// </summary>
    private string _fileExtensions;
    public string FileExtensions
    {
        get => _fileExtensions;
        set
        {
            _fileExtensions = NormalizeFileExtensions(value);
            ParsedFileExtensions = ParseFileExtensions(_fileExtensions);
        }
    }

    /// <summary>
    /// 파싱된 확장자 목록
    /// </summary>
    public HashSet<string> ParsedFileExtensions { get; private set; } = new();


    #endregion

    #region 동기화 주기 (SyncInterval)

    /// <summary>
    /// 동기화 주기
    /// </summary>
    private string _syncInterval;
    public string SyncInterval
    {
        get => _syncInterval;
        set
        {
            if (TimeSpanHelper.IsValidTimeSpanFormat(value))
            {
                _syncInterval = TimeSpanHelper.ParseTimeSpan(value).ToString();
            }
            else
            {
                throw new ArgumentException("Invalid TimeSpan format. Expected format: [dd.]hh:mm:ss");
            }
        }
    }
    public TimeSpan SyncIntervalTimeSpan => TimeSpanHelper.ParseTimeSpan(SyncInterval);

    #endregion

    /// <summary>
    /// 평면구조 동기화 여부
    /// </summary>
    public bool UseFlatStructure { get; set; } = false;

    #region 중복 파일 처리 방식

    public enum DuplicateFileHandling
    {
        /// <summary>
        /// 다른 경우만 덮어쓰기
        /// </summary>
        ReplaceIfDifferent,
        /// <summary>
        /// 중복 파일 덮어쓰기
        /// </summary>
        Replace,
        /// <summary>
        /// 중복 파일 건너뛰기
        /// </summary>
        Skip,
        /// <summary>
        /// 둘 다 유지 (파일명 변경)
        /// </summary>
        KeepBoth
    }

    /// <summary>
    /// 중복 파일 처리 방식
    /// </summary>
    public DuplicateFileHandling DuplicateHandling { get; set; } = DuplicateFileHandling.ReplaceIfDifferent;

    #endregion
    
    public SyncSettings()
    {
        RemoteLocation = string.Empty;
        Username = string.Empty;
        Password = string.Empty;
        LocalLocation = string.Empty;
        FileExtensions = string.Empty;
        SyncInterval = ConstSettings.DefaultSyncInterval;
    }
    
    /// <summary>
    /// 설정 유효성 검사
    /// </summary>
    /// <returns></returns>
    public (bool IsValid, string ErrorMessage) Validate()
    {
        if (string.IsNullOrWhiteSpace(RemoteLocation))
        {
            return (false, "Remote location is required");
        }

        if (string.IsNullOrWhiteSpace(LocalLocation))
        {
            return (false, "Local location is required");
        }
        
        if (!Directory.Exists(LocalLocation))
        {
            return (false, "Local location does not exist");
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            return (false, "Username is required");
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            return (false, "Password is required");
        }

        if (string.IsNullOrWhiteSpace(FileExtensions))
        {
            var invalidExtensions = FileExtensions
                .Split(',')
                .Where(ext => !Regex.IsMatch(ext, @"^\.[a-zA-Z0-9]+$"))
                .ToList();
            
            if (invalidExtensions.Any())
            {
                return (false, $"Invalid file extension format: {string.Join(", ", invalidExtensions)}");
            }
        }

        if (!TimeSpanHelper.IsValidTimeSpanFormat(SyncInterval))
        {
            return (false, "Invalid sync interval format");
        }

        return (true, string.Empty);
    }
    
    /// <summary>
    /// 파일이 확장자 목록에 포함되는지 확인 
    /// </summary>
    /// <param name="fileName">전체 파일명</param>
    /// <returns>포함 여부</returns>
    public bool ShouldSyncFile(string fileName)
    {
        if (!ParsedFileExtensions.Any())
        {
            return true;
        }

        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        return ParsedFileExtensions.Contains(extension);
    }
    
    /// <summary>
    /// 현재 시간에 맞춰 평가된 폴더 패턴 반환
    /// </summary>
    /// <returns></returns>
    public string GetExpectedFolderName()
    {
        if (string.IsNullOrWhiteSpace(_folderPattern))
        {
            return string.Empty;
        }
        
        var today = DateTime.Now;
        var result = _folderPattern;
        var matches = PatternRegex.Matches(result);
        
        // 끝에서부터 처리
        for (int i = matches.Count - 1; i >= 0; i--)
        {
            var match = matches[i];
            string format = match.Groups[1].Value;
            result = result.Remove(match.Index, match.Length)
                .Insert(match.Index, today.ToString(format));
        }

        return result;
    }
    
    /// <summary>
    /// 해당 경로가 동기화 대상인지 확인
    /// </summary>
    /// <param name="folderPath">폴더 경로</param>
    /// <returns></returns>
    public bool ShouldSyncFolder(string folderPath)
    {
        // 패턴이 없으면 모든 폴더 동기화
        if (string.IsNullOrWhiteSpace(_folderPattern))
        {
            return true;
        }

        string folderName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar));
        string expectedFolderName = GetExpectedFolderName();
        
        return string.Equals(folderName, expectedFolderName, StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// 텍스트 내 패턴 카운트 반환
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public int GetPatternCount(string text)
    {
        return PatternRegex.Matches(text).Count;
    }
    
    /// <summary>
    /// 텍스트 내 패턴 포맷 반환
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public IEnumerable<string> GetPatternFormats(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }
        
        var matches = PatternRegex.Matches(text);
        foreach (Match match in matches)
        {
            yield return match.Groups[1].Value;
        }
    }
    
    /// <summary>
    /// 파일 확장자 문자열 정규화
    /// </summary>
    /// <param name="extensions">파일 확장자 연결 문자열</param>
    /// <returns>정규화된 확장자 문자열</returns>
    private string NormalizeFileExtensions(string extensions)
    {
        if (string.IsNullOrWhiteSpace(extensions))
            return string.Empty;
        
        // 확장자 분리 및 정규화
        var normalized = extensions
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(ext =>
            {
                ext = ext.Trim().ToLowerInvariant();
                return ext.StartsWith(".") ? ext : $".{ext}";
            })
            .Distinct()
            .OrderBy(ext => ext);

        return string.Join(",", normalized);
    }
    
    /// <summary>
    /// 파일 확장자 파싱
    /// </summary>
    /// <param name="extensions">정규화 된 확장자 문자열</param>
    /// <returns>파싱된 확장자 목록</returns>
    private HashSet<string> ParseFileExtensions(string extensions)
    {
        if (string.IsNullOrWhiteSpace(extensions))
        {
            return new HashSet<string>();
        }

        return extensions.Split(',').ToHashSet();
    }

    /// <summary>
    /// 폴더 패턴 유효성 검사
    /// </summary>
    /// <exception cref="ArgumentException"></exception>
    private void ValidateFolderPattern()
    {
        if (string.IsNullOrWhiteSpace(FolderPattern))
        {
            return;
        }

        try
        {
            var testDate = new DateTime(2024, 1, 1);
            var matches = PatternRegex.Matches(FolderPattern);

            if (matches.Count == 0 && !string.IsNullOrWhiteSpace(_folderPattern))
            {
                return;
            }

            foreach (Match match in matches)
            {
                string format = match.Groups[1].Value;
                try
                {
                    testDate.ToString(format);
                }
                catch (Exception e)
                {
                    throw new ArgumentException($"Invalid folder pattern: {e.Message}");
                }
            }
        }
        catch (Exception e)
        {
            throw new ArgumentException($"Invalid folder pattern: {e.Message}");
        }
    }

    public override bool Equals(object? obj)
    {
        // 설정들 비교
        if (obj == null)
        {
            return false;
        }

        if (obj is not SyncSettings other)
        {
            return false;
        }

        SyncSettings others = obj as SyncSettings;

        return RemoteLocation == others.RemoteLocation &&
               Username == others.Username &&
               Password == others.Password &&
               LocalLocation == others.LocalLocation &&
               FolderPattern == others.FolderPattern &&
               FileExtensions == others.FileExtensions &&
               SyncInterval == others.SyncInterval &&
               UseFlatStructure == others.UseFlatStructure &&
               DuplicateHandling == others.DuplicateHandling;
    }
}

public static class ConstSettings
{
    public const string Settings = "Settings";
    
    public const string Username = "Username";
    public const string Password = "Password";
    public const string RemoteLocation = "RemoteLocation";
    public const string LocalLocation = "LocalLocation";
    public const string FolderPattern = "FolderPattern";
    public const string FileExtensions = "FileExtensions";
    public const string SyncInterval = "SyncInterval";
    public const string UseFlatStructure = "UseFlatStructure";
    public const string DuplicateHandling = "DuplicateHandling";
    
    public const string DefaultSyncInterval = "1.00:00:00";
}