using System;
using System.Threading;
using System.Threading.Tasks;

namespace NamedPipeLine.Interfaces
{
    public interface IIPCClient<T> where T: IIPCMessage
    {
        /// <summary>
        /// 서버로부터 메시지를 수신했을 때 발생하는 이벤트
        /// </summary>
        event EventHandler<T>? MessageReceived;
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
}