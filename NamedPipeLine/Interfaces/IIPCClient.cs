using System;
using System.Threading.Tasks;

namespace NamedPipeLine.Interfaces
{
    public interface IIPCClient<T> where T: IIPCMessage
    {
        event EventHandler<T>? MessageReceived;
        Task ConnectAsync();
        Task DisconnectAsync();
        Task SendMessageAsync(T message);
    }
}