using System;
using System.IO;
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
            _pipeServerStream = new NamedPipeServerStream(
                pipeName, 
                PipeDirection.InOut, 
                1,
                PipeTransmissionMode.Message, 
                PipeOptions.Asynchronous
                );
            _cancellationToken = new CancellationTokenSource();
        }
        
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken.Token, cancellationToken);
            _isRunning = true;

            while (_isRunning)
            {
                try
                {
                    await _pipeServerStream.WaitForConnectionAsync(linkedCancellation.Token);
                    await ProcessClientMessageAsync(linkedCancellation.Token);
                }
                catch (OperationCanceledException) when (linkedCancellation.Token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception e)
                {
                    await Task.Delay(1000, linkedCancellation.Token);
                }
                finally
                {
                    if (_pipeServerStream.IsConnected)
                    {
                        _pipeServerStream.Disconnect();
                    }
                }
            }
        }

        public Task StopAsync()
        {
            _isRunning = false;
            _cancellationToken.Cancel();
            return Task.CompletedTask; 
        }

        public async Task SendMessageAsync(T message)
        {
            if (!_pipeServerStream.IsConnected)
            {
                return;
            }

            try
            {
                using var writer = new StreamWriter(_pipeServerStream){ AutoFlush = true };
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
            _pipeServerStream.Dispose();
            _cancellationToken.Dispose();
        }
        
        private async Task ProcessClientMessageAsync(CancellationToken linkedCancellationToken)
        {
            using var reader = new StreamReader(_pipeServerStream);
            using var writer = new StreamWriter(_pipeServerStream){ AutoFlush = true };

            while (_pipeServerStream.IsConnected && _isRunning && !linkedCancellationToken.IsCancellationRequested)
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
        }

    }
}