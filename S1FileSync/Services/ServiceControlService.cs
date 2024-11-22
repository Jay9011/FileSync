using System.ServiceProcess;
using S1FileSync.Services.Interface;
using S1FileSync.ViewModels;

namespace S1FileSync.Services;

public class ServiceControlService : PropertyChangeNotifier, IServiceControlService
{
    private const string ServiceName = "S1FileSyncService";
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private ServiceControllerStatus _status = ServiceControllerStatus.Stopped;
    public ServiceControllerStatus Status
    {
        get => _status; 
        private set => SetField(ref _status, value);
    }
    
    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }
    
    public async Task<(bool success, string message)> StartServiceAsync()
    {
        try
        {
            using var service = new ServiceController(ServiceName);
            await RefreshServiceStatusAsync();

            if (service.Status == ServiceControllerStatus.Running)
            {
                return (true, "Service is already running");
            }

            if (service.Status == ServiceControllerStatus.StartPending)
            {
                return (true, "Service is starting");
            }

            service.Start();
            await RefreshServiceStatusAsync();
            await WaitForStatusAsync(service, ServiceControllerStatus.Running);
            await RefreshServiceStatusAsync();
            
            return (true, "Service started successfully");
        }
        catch (InvalidOperationException)
        {
            StatusMessage = "Service not found";
            return (false, "Service not found");
        }
        catch (Exception e)
        {
            await RefreshServiceStatusAsync();
            return (false, $"Failed to start service: {e.Message}");
        }
    }

    public async Task<(bool success, string message)> StopServiceAsync()
    {
        try
        {
            using var service = new ServiceController(ServiceName);
            await RefreshServiceStatusAsync();

            if (service.Status == ServiceControllerStatus.Stopped)
            {
                return (true, "Service is already stopped");
            }

            if (service.Status == ServiceControllerStatus.StopPending)
            {
                return (true, "Service is stopping");
            }

            service.Stop();
            await RefreshServiceStatusAsync();
            await WaitForStatusAsync(service, ServiceControllerStatus.Stopped);
            await RefreshServiceStatusAsync();
            
            return (true, "Service stopped successfully");
        }
        catch (InvalidOperationException)
        {
            StatusMessage = "Service not found";
            return (false, "Service not found");
        }
        catch (Exception e)
        {
            await RefreshServiceStatusAsync();
            return (false, $"Failed to stop service: {e.Message}");
        }
    }
    
    /// <summary>
    /// 서비스 상태를 갱신
    /// </summary>
    /// <returns></returns>
    public async Task<ServiceControllerStatus?> RefreshServiceStatusAsync()
    {
        using var service = new ServiceController(ServiceName);
        await Task.Run(() => service.Refresh());
        Status = service.Status;
        StatusMessage = Status.ToString();
        
        return Status;
    }

    /// <summary>
    /// 서비스 상태를 확인
    /// </summary>
    /// <returns></returns>
    public async Task<ServiceControllerStatus?> GetServiceStatusAsync()
    {
        try
        {
            return await RefreshServiceStatusAsync();
        }
        catch (Exception e)
        {
            StatusMessage = $"Error: {e.Message}";
            return null;
        }
    }
    
    private async Task WaitForStatusAsync(ServiceController service, ServiceControllerStatus desiredStatus)
    {
        await Task.Run(() =>
        {
            service.WaitForStatus(desiredStatus, _timeout);
        });
    }

}