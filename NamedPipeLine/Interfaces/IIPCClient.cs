using System;
using System.Threading;
using System.Threading.Tasks;
using NamedPipeLine.Models;

namespace NamedPipeLine.Interfaces
{
    public interface IIPCClient<T> where T: class, IIPCMessage, new()
    {
        /// <summary>
        /// 클라이언트가 서버와 연결되어 있는지 여부
        /// </summary>
        public bool IsConnected { get; }
        /// <summary>
        /// 서버로부터 메시지를 수신했을 때 발생하는 이벤트
        /// </summary>
        event EventHandler<T>? MessageReceived;
        /// <summary>
        /// 서버 연결 상태가 변경되었을 때 발생하는 이벤트
        /// </summary>
        event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
        /// <summary>
        /// 서버 연결
        /// </summary>
        /// <returns></returns>
        Task ConnectAsync(CancellationToken cancellationToken = default);
        /// <summary>
        /// 서버 연결 해제
        /// </summary>
        /// <returns></returns>
        Task DisconnectAsync();

        /// <summary>
        /// 서버로 메시지 전송
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        Task SendMessageAsync(T message);
    }

    /// <summary>
    /// 서버 연결 상태 변경 이벤트 인자 클래스
    /// </summary>
    public class ConnectionStateChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; }
        public string? ErrorMessage { get; set; }

        public ConnectionStateChangedEventArgs(bool isConnected, string? errorMessage = null)
        {
            IsConnected = isConnected;
            ErrorMessage = errorMessage;
        }
    }
}