using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using NamedPipeLine.Interfaces;
using NamedPipeLine.Models;

namespace NamedPipeLine.Services
{
    public class NamedPipeServer<T> : IIPCServer<T>, IDisposable where T : IIPCMessage
    {
        public event EventHandler<T>? MessageReceived;
        
        private readonly string _pipeName;
        private readonly NamedPipeServerStream _pipeServerStream;
        private readonly IMessageSerializer _serializer;
        private readonly CancellationTokenSource _cancellationToken;
        private bool _isRunning;

        public NamedPipeServer(string pipeName, IMessageSerializer? serializer = null)
        {
            _pipeName = pipeName;
            _serializer = serializer ?? new JsonMessageSerializer();
        }
        
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task StopAsync()
        {
            throw new NotImplementedException();
        }

        public Task SendMessageAsync(T message)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}