using NamedPipeLine.Models;

namespace S1FileSync.Models;

/// <summary>
/// 파일 동기화 메시지 타입
/// </summary>
public enum FileSyncMessageType
{
    ProgressUpdate,
    StatusChange,
    Error,
    ServiceCommand,
    ConnectionStatus
}

/// <summary>
/// 파일 동기화 상태 타입
/// </summary>
public enum FileSyncStatusType
{
    SyncStart,
    SyncEnd,
    SyncError
}

/// <summary>
/// 원격지 연결 상태
/// </summary>
public enum ConnectionStatusType
{
    Connected,
    Disconnected
}

/// <summary>
/// 파일 동기화 진행 상태
/// </summary>
/// <param name="FileName">파일명</param>
/// <param name="FileSize">파일 크기</param>
/// <param name="Progress">진행도</param>
/// <param name="Speed">속도</param>
/// <param name="IsCompleted">완료여부</param>
public record FileSyncProgress(
    string FileName,
    long FileSize,
    double Progress,
    double Speed,
    bool IsCompleted
);

/// <summary>
/// 파일 동기화 메시지 내용
/// </summary>
public record FileSyncContent
{
    public FileSyncMessageType Type { get; init; }
    public string? Message { get; init; }
    public FileSyncProgress? Progress { get; init; }
}

/// <summary>
/// 파일 동기화 메시지
/// </summary>
public class FileSyncMessage : IPCMessage<FileSyncContent>
{
    public FileSyncMessage(FileSyncMessageType type, string? message = null, FileSyncProgress? progress = null)
    {
        Content = new FileSyncContent
        {
            Type = type,
            Message = message,
            Progress = progress
        };
    }

    public FileSyncMessage(FileSyncMessageType type, ConnectionStatusType statusType, string? message = null)
    {
        Content = new FileSyncContent
        {
            Type = type,
            Message = MakeStatusMessage(statusType, message),
            Progress = null
        };
    }

    /// <summary>
    /// 상태 메시지 생성
    /// </summary>
    /// <param name="statusType"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public string MakeStatusMessage(ConnectionStatusType statusType, string? message = null)
    {
        return statusType switch
        {
            ConnectionStatusType.Connected => "{{Connected}}" + message,
            ConnectionStatusType.Disconnected => "{{Disconnected}}" + message,
            _ => message ?? string.Empty
        };
    }
    
    /// <summary>
    /// 연결 상태 타입 반환
    /// </summary>
    /// <returns></returns>
    public ConnectionStatusType GetConnectionStatusType()
    {
        if (Content.Type != FileSyncMessageType.ConnectionStatus)
        {
            return ConnectionStatusType.Disconnected;
        }
        
        return Content.Message switch
        {
            string msg when msg.StartsWith("{{Connected}}") => ConnectionStatusType.Connected,
            string msg when msg.StartsWith("{{Disconnected}}") => ConnectionStatusType.Disconnected,
            _ => ConnectionStatusType.Disconnected
        };
    }
    
    /// <summary>
    /// 연결 상태 메시지 반환
    /// </summary>
    /// <returns></returns>
    public string GetConnectionStatusMessage()
    {
        if (Content.Type != FileSyncMessageType.ConnectionStatus)
        {
            return string.Empty;
        }

        return Content.Message?.Replace("{{Connected}}", string.Empty).Replace("{{Disconnected}}", string.Empty) ?? string.Empty;
    }

    public FileSyncMessage()
    {
    }
}