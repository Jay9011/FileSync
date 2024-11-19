using System;

namespace NamedPipeLine.Models
{
    public enum PipeCommunicationFailureType
    {
        ConnectionFailed,
        SendFailed,
        ReceiveFailed,
        PipeDisconnected,
        Unknown
    }
    
    public class PipeCommunicationException : Exception
    {
        public PipeCommunicationFailureType FailureType { get; }
        
        public PipeCommunicationException(string message, PipeCommunicationFailureType failureType, Exception? innerException = null) : base(message, innerException)
        {
            FailureType = failureType;
        }
    }
}