using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using S1FileSync.Helpers;

namespace S1FileSync.Models;

public class SyncSettings
{
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

    #region 폴더 패턴 관련(FolderPattern)

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
                // 이스케이프 문자 제거 시도
                value = value.Replace("\\{", "{").Replace("\\}", "}");
            }
        
            _folderPattern = value;
            UpdateFolderPatternRegex();
        }
    }
    
    /// <summary>
    /// 컴파일 된 폴더 패턴 정규식
    /// </summary>
    private Regex? _folderPatternRegex;
    
    /// <summary>
    /// 마지막 패턴 평가 시점
    /// </summary>
    private DateTime _lastPatternEvaluation = DateTime.Now;

    /// <summary>
    /// 현재 평가된 폴더 패턴
    /// </summary>
    private string _evaluatedFolderPattern;

    #endregion

    #region 확장자 관련 (FileExtensions)

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

    #region 동기화 관련 (SyncInterval)

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

        if (!string.IsNullOrWhiteSpace(FolderPattern))
        {
            try
            {
                var testPattern = GetEvaluatedFolderPattern();
                if (string.IsNullOrEmpty(testPattern))
                {
                    return (false, "Invalid folder pattern");
                }
            }
            catch (Exception e)
            {
                return (false, $"Invalid folder pattern: {e.Message}");
            }
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
    public string GetEvaluatedFolderPattern()
    {
        var now = DateTime.Now;
        
        // 1분에 한 번만 패턴 재평가
        if ((now - _lastPatternEvaluation).TotalMinutes < 1 && !string.IsNullOrEmpty(_evaluatedFolderPattern))
        {
            return _evaluatedFolderPattern;
        }

        if (string.IsNullOrWhiteSpace(_folderPattern))
        {
            return string.Empty;
        }
        
        _evaluatedFolderPattern = Regex.Replace(_folderPattern, @"\{\{(.+?)\}\}", match =>
        {
            string format = match.Groups[1].Value;
            return now.ToString(format);
        });
        
        _lastPatternEvaluation = now;
        return _evaluatedFolderPattern;
    }
    
    /// <summary>
    /// 해당 경로가 동기화 대상인지 확인
    /// </summary>
    /// <param name="folderPath">폴더 경로</param>
    /// <returns></returns>
    public bool ShouldSyncFolder(string folderPath)
    {
        // 패턴이 없으면 모든 폴더 동기화
        if (_folderPatternRegex == null)
        {
            return true;
        }

        string folderName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar));
        return _folderPatternRegex.IsMatch(folderName);
    }
    
    /// <summary>
    /// 폴더 패턴 정규식 업데이트
    /// </summary>
    private void UpdateFolderPatternRegex()
    {
        if (string.IsNullOrWhiteSpace(_folderPattern))
        {
            _folderPatternRegex = null;
            return;
        }

        try
        {
            // 이중 중괄호 사이의 날짜 포맷을 숫자 패턴으로 변환
            int startIndex = _folderPattern.IndexOf("{{", StringComparison.Ordinal);
            int endIndex = _folderPattern.IndexOf("}}", StringComparison.Ordinal);
        
            if (startIndex == -1 || endIndex == -1)
            {
                // 날짜 패턴이 없는 경우 문자열 그대로 매칭
                _folderPatternRegex = new Regex($"^{Regex.Escape(_folderPattern)}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                return;
            }
            string dateFormat = _folderPattern.Substring(startIndex + 2, endIndex - startIndex - 2);
        
            string prefix = Regex.Escape(_folderPattern.Substring(0, startIndex));
            string suffix = Regex.Escape(_folderPattern.Substring(endIndex + 2));
        
            string datePattern = GetDateTimeRegexPattern(dateFormat);
        
            // 최종 패턴 조합
            string pattern = $"^{prefix}{datePattern}{suffix}$";
        
            _folderPatternRegex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            _evaluatedFolderPattern = pattern;
        }
        catch (Exception e)
        {
            _folderPatternRegex = null;
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
    /// 날짜/시간 포멧을 정규식 패턴으로 변환
    /// </summary>
    /// <param name="format">yyyyMMddHHmmss 형식의 날짜/시간 포멧</param>
    /// <returns>정규식 패턴</returns>
    private string GetDateTimeRegexPattern(string format)
    {
        var pattern = format
            .Replace("yyyy", @"\d{4}")
            .Replace("yy", @"\d{2}")
            .Replace("MM", @"\d{2}")
            .Replace("dd", @"\d{2}")
            .Replace("HH", @"\d{2}")
            .Replace("hh", @"\d{2}")
            .Replace("mm", @"\d{2}")
            .Replace("ss", @"\d{2}");
    
        return pattern;
    }
}

public static class ConstSettings
{
    public const string Settings = "Settings";
    public const string RemoteLocation = "RemoteLocation";
    public const string Username = "Username";
    public const string Password = "Password";
    public const string LocalLocation = "LocalLocation";
    public const string FileExtensions = "FileExtensions";
    public const string SyncInterval = "SyncInterval";
    public const string DefaultSyncInterval = "1.00:00:00";
}