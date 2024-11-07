using Microsoft.Extensions.Logging;
using NamedPipeLine.Interfaces;
using NamedPipeLine.Services;
using S1FileSync.Models;

namespace S1FileSync.Services;

public class FileSyncIPCService : IDisposable
{
    private const string PipeName = "S1FileSyncPipe";
    private readonly IIPCServer<FileSyncMessage> _server;
    private readonly ILogger<FileSyncIPCService> _logger;
    
    public event EventHandler<FileSyncMessage>? MessageReceived;

    public FileSyncIPCService(ILogger<FileSyncIPCService> logger)
    {
        _logger = logger;
        _server = new NamedPipeServer<FileSyncMessage>(PipeName);
        _server.MessageReceived += OnMessageReceived;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _server.StartAsync(cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred while starting the IPC server");
            throw;
        }
    }

    public async Task SendMessageAsync(FileSyncMessage message)
    {
        try
        {
            await _server.SendMessageAsync(message);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred while sending a message");
        }
    }

    public void Dispose()
    {
        if (_server is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
    
    private void OnMessageReceived(object? sender, FileSyncMessage message)
    {
        MessageReceived?.Invoke(this, message);
    }

}