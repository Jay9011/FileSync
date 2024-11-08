using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
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
        private readonly IMessageSerializer _serializer;
        private readonly CancellationTokenSource _cancellationToken;
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
        private bool _isRunning;
        
        private NamedPipeServerStream? _pipeServerStream;
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
                    using var pipeServer = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous);

                    await pipeServer.WaitForConnectionAsync(linkecCancellationToken.Token);

                    await ProcessClientMessageAsync(pipeServer, linkecCancellationToken.Token);
                }
                catch (OperationCanceledException) when(linkecCancellationToken.Token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception)
                {
                    await Task.Delay(1000, linkecCancellationToken.Token);
                }
            }
        }

        public async Task SendMessageAsync(T message)
        {
            try
            {
                using var pipeServer = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.Out,
                    1,
                    PipeTransmissionMode.Message,
                    PipeOptions.Asynchronous);
                
                using var timeoutCancellationTokenSource = new CancellationTokenSource(100);

                try
                {
                    await pipeServer.WaitForConnectionAsync(timeoutCancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (pipeServer.IsConnected)
                {
                    using var writer = new StreamWriter(_pipeServerStream) { AutoFlush = true };
                    string messageText = _serializer.Serialize(message);
                    await writer.WriteLineAsync(messageText);
                }
            }
            catch (Exception)
            {
                return;
            }
        }

        private async Task ProcessClientMessageAsync(NamedPipeServerStream pipeServer, CancellationToken linkedCancellationToken)
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
                    MessageReceived?.Invoke(this, message);
                }
                catch (Exception)
                {
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

            if (_pipeServerStream is { IsConnected: true })
            {
                _pipeServerStream.Disconnect();
            }
        }

        public void Dispose()
        {
            _isRunning = false;
            _cancellationToken.Cancel();
            _pipeServerStream?.Dispose();
            _connectionLock.Dispose();
            _cancellationToken.Dispose();
        }
    }
}