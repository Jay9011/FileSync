using System;

namespace NamedPipeLine.Interfaces
{
    public interface IIPCMessage
    {
        /// <summary>
        /// 메시지 식별자
        /// </summary>
        public string MessageId { get; }
        /// <summary>
        /// 메시지 타입
        /// </summary>
        public PipeMessageType MessageType { get; set; }
        /// <summary>
        /// 메시지 생성 시간
        /// </summary>
        public DateTime Timestamp { get; }
    }
    
    /// <summary>
    /// 파이프 시스템 메시지 타입
    /// </summary>
    public enum PipeMessageType
    {
        UserMessage,
        PipeChanged,
        ServiceCommand,
    }
}