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
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
        private bool _isRunning;
        private bool _isConnected;
        
        private Task? _messageListenerTask;
        
        public bool IsConnected => _isConnected && _pipeClientStream.IsConnected;
        
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
            await _connectionLock.WaitAsync(cancellationToken);
            try
            {
                if (_isRunning && _pipeClientStream.IsConnected)
                {
                    return;
                }

                if (!_pipeClientStream.IsConnected)
                {
                    await _pipeClientStream.ConnectAsync(cancellationToken);
                }
                
                _isConnected = true;
                _isRunning = true;
                _messageListenerTask = StartListeningAsync(cancellationToken);
            }
            catch (Exception e)
            {
                if (_pipeClientStream.IsConnected)
                {
                    _pipeClientStream.Close();
                }
                _isConnected = false;
                throw;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task SendMessageAsync(T message)
        {
            if (!_pipeClientStream.IsConnected)
            {
                throw new InvalidOperationException("Client is not connected.");
            }

            try
            {
                using var writer = new StreamWriter(_pipeClientStream) { AutoFlush = true };
                string messageText = _serializer.Serialize(message);
                await writer.WriteLineAsync(messageText);
            }
            catch (Exception e)
            {
                if (_pipeClientStream.IsConnected)
                {
                    _pipeClientStream.Close();
                }

                throw;
            }
        }

        private async Task StartListeningAsync(CancellationToken cancellationToken)
        {
            using var linkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken.Token, cancellationToken);
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
                    _isConnected = false;
                    break;
                }
                catch (Exception e)
                {
                    _isConnected = false;
                    if (_pipeClientStream.IsConnected)
                    {
                        _pipeClientStream.Close();
                    }
                    break;
                }
            }
            
            _isConnected = false;
        }

        public async Task DisconnectAsync()
        {
            await _connectionLock.WaitAsync();
            try
            {
                _isRunning = false;
                _isConnected = false;

                if (_pipeClientStream.IsConnected)
                {
                    _pipeClientStream.Close();
                }

                if (_messageListenerTask != null)
                {
                    await _messageListenerTask;
                }
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public void Dispose()
        {
            _isRunning = false;
            _cancellationToken.Cancel();
            _connectionLock.Dispose();
            _pipeClientStream.Close();
            _pipeClientStream.Dispose();
            _cancellationToken.Dispose();
        }
    }
}