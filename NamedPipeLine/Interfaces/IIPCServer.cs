using System;
using System.Threading;
using System.Threading.Tasks;

namespace NamedPipeLine.Interfaces
{
    public interface IIPCServer<T> where T : IIPCMessage
    {
        event EventHandler<T>? MessageReceived;
        Task StartAsync(CancellationToken cancellationToken = default);
        Task StopAsync();
        Task SendMessageAsync(T message);
    }
}