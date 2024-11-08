using System;
using System.Collections.Concurrent;
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
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
        private bool _isRunning;
        
        private Task? _messageListenerTask;

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
            await _connectionLock.WaitAsync(cancellationToken);

            try
            {
                if (_isRunning)
                {
                    return;
                }

                _isRunning = true;
                _messageListenerTask = StartListeningAsync(cancellationToken);
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private async Task StartListeningAsync(CancellationToken cancellationToken)
        {
            using var linkecCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken.Token, cancellationToken);

            while (_isRunning && !linkecCancellationToken.Token.IsCancellationRequested)
            {
                try
                {
                    if (!_pipeServerStream.IsConnected)
                    {
                        await _pipeServerStream.WaitForConnectionAsync(linkecCancellationToken.Token);
                    }

                    await ProcessClientMessageAsync(linkecCancellationToken.Token);
                }
                catch (OperationCanceledException) when(linkecCancellationToken.Token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception e)
                {
                    if (_pipeServerStream.IsConnected)
                    {
                        _pipeServerStream.Disconnect();
                    }
                    await Task.Delay(1000, linkecCancellationToken.Token);
                }
            }
        }

        public async Task SendMessageAsync(T message)
        {
            if (!_pipeServerStream.IsConnected)
            {
                throw new InvalidOperationException("Pipe is not connected");
            }

            try
            {
                using var writer = new StreamWriter(_pipeServerStream) { AutoFlush = true };
                string messageText = _serializer.Serialize(message);
                await writer.WriteLineAsync(messageText);
            }
            catch (Exception e)
            {
                if (_pipeServerStream.IsConnected)
                {
                    _pipeServerStream.Disconnect();
                }

                throw;
            }
        }

        private async Task ProcessClientMessageAsync(CancellationToken linkedCancellationToken)
        {
            using var reader = new StreamReader(_pipeServerStream);

            while (_pipeServerStream.IsConnected && _isRunning && !linkedCancellationToken.IsCancellationRequested)
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
                catch (Exception e)
                {
                    if (_pipeServerStream.IsConnected)
                    {
                        _pipeServerStream.Disconnect();
                    }

                    break;
                }
            }
        }

        public async Task StopAsync()
        {
            _isRunning = false;
            _cancellationToken.Cancel();

            if (_messageListenerTask != null)
            {
                await _messageListenerTask;
            }

            if (_pipeServerStream.IsConnected)
            {
                _pipeServerStream.Disconnect();
            }
        }

        public void Dispose()
        {
            _isRunning = false;
            _cancellationToken.Cancel();
            _pipeServerStream.Dispose();
            _connectionLock.Dispose();
            _cancellationToken.Dispose();
        }
    }
}