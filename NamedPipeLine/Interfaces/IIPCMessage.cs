using System;

namespace NamedPipeLine.Interfaces
{
    public interface IIPCMessage
    {
        /// <summary>
        /// 메시지 식별자
        /// </summary>
        string MessageId { get;}
        /// <summary>
        /// 메시지 타입
        /// </summary>
        string MessageType { get; }
        /// <summary>
        /// 메시지 생성 시간
        /// </summary>
        DateTime Timestamp { get; }
    }
}