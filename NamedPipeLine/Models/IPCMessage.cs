using System;
using NamedPipeLine.Interfaces;

namespace NamedPipeLine.Models
{
    /// <summary>
    /// 파이프 시스템 메시지
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class IPCMessage<T> : IIPCMessage where T : class
    {
        public string MessageId { get; } = Guid.NewGuid().ToString();
        public PipeMessageType MessageType { get; set; } = PipeMessageType.UserMessage;
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public T? Content { get; set; }
    }

}