using System;
using NamedPipeLine.Interfaces;

namespace NamedPipeLine.Models
{
    public class IPCMessage<T> : IIPCMessage where T : class
    {
        public string MessageId { get; } = Guid.NewGuid().ToString();
        public string MessageType { get; set; } = string.Empty;
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public T? Content { get; set; }
    }
}