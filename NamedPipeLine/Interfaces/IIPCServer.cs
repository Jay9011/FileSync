using System;
using System.Threading;
using System.Threading.Tasks;
using NamedPipeLine.Models;

namespace NamedPipeLine.Interfaces
{
    public interface IIPCServer<T> where T : class, IIPCMessage, new()
    {
        /// <summary>
        /// 클라이언트로부터 메시지를 수신했을 때 발생하는 이벤트
        /// </summary>
        event EventHandler<T>? MessageReceived;
        /// <summary>
        /// 서버 시작
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task StartAsync(CancellationToken cancellationToken = default);
        /// <summary>
        /// 서버 중지
        /// </summary>
        /// <returns></returns>
        Task StopAsync();

        /// <summary>
        /// 클라이언트로 메시지를 전송
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        Task SendMessageAsync(T message);
    }
}