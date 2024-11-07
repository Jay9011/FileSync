using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using NamedPipeLine.Interfaces;
using NamedPipeLine.Models;

namespace NamedPipeLine.Services
{
    public class NamedPipeClient<T> : IIPCClient<T>, IDisposable where T : IIPCMessage
    {
        public event EventHandler<T>? MessageReceived;

        private readonly string _pipeName;
        private readonly string _serverName;
        private readonly NamedPipeClientStream _pipeClientStream;
        private readonly IMessageSerializer _serializer;
        private readonly CancellationTokenSource _cancellationToken;
        private bool _isRunning;
        
        public NamedPipeClient(string pipeName, string serverName = ".", IMessageSerializer? serializer = null)
        {
            _pipeName = pipeName;
            _serverName = serverName;
            _serializer = serializer ?? new JsonMessageSerializer();
            _pipeClientStream = new NamedPipeClientStream(serverName, pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            _cancellationToken = new CancellationTokenSource();
        }
        
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken.Token, cancellationToken);
            await _pipeClientStream.ConnectAsync(linkedCancellation.Token);
            _isRunning = true;
            _ = StartListeningAsync(linkedCancellation.Token);
        }

        public async Task DisconnectAsync()
        {
            _isRunning = false;
            if (_pipeClientStream.IsConnected)
            {
                _pipeClientStream.Close();
            }

            await Task.CompletedTask;
        }

        public async Task SendMessageAsync(T message)
        {
            if (!_pipeClientStream.IsConnected)
            {
                return;
            }

            try
            {
                using var writer = new StreamWriter(_pipeClientStream) { AutoFlush = true };
                string messageText = _serializer.Serialize(message);
                await writer.WriteLineAsync(messageText);
            }
            catch (Exception e)
            {
                return;
            }
        }

        public void Dispose()
        {
            _isRunning = false;
            _cancellationToken.Cancel();
            _pipeClientStream.Dispose();
            _cancellationToken.Dispose();
        }
        
        private async Task StartListeningAsync(CancellationToken linkedCancellationToken)
        {
            using var reader = new StreamReader(_pipeClientStream);

            while (_isRunning && _pipeClientStream.IsConnected && !linkedCancellationToken.IsCancellationRequested)
            {
                try
                {
                    string? messageText = await reader.ReadLineAsync();
                    if (messageText == null)
                    {
                        break;
                    }

                    var message = _serializer.Deserialize<T>(messageText);
                    if (message != null)
                    {
                        MessageReceived?.Invoke(this, message);
                    }
                }
                catch (OperationCanceledException) when (linkedCancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception e)
                {
                    break;
                }
            }
        }

    }
}