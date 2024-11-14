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

    public FileSyncMessage()
    {
    }
}