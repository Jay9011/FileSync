﻿using S1FileSync.Models;

namespace S1FileSyncService.Services.Interfaces;

public interface ISendMessage
{
    /// <summary>
    /// Client에게 메시지를 전송
    /// </summary>
    /// <param name="messageType">메시지 타입</param>
    /// <param name="message">메시지 내용</param>
    /// <param name="cancellationToken"></param>
    Task SendMessageAsync(FileSyncMessageType messageType, string message, CancellationToken cancellationToken = default);
    /// <summary>
    /// Client에게 메시지를 전송
    /// </summary>
    /// <param name="messageType">메시지 타입</param>
    /// <param name="connectionStatusType">연결 상태</param>
    /// <param name="message">연결 상태 메시지</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task SendMessageAsync(FileSyncMessageType messageType, ConnectionStatusType connectionStatusType, string message, CancellationToken cancellationToken);
}