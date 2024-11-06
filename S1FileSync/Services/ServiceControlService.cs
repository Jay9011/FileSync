using System.ServiceProcess;
using S1FileSync.Services.Interface;

namespace S1FileSync.Services;

public class ServiceControlService : IServiceControlService
{
    private const string ServiceName = "S1FileSyncService";
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);
    
    public async Task<(bool success, string message)> StartServiceAsync()
    {
        try
        {
            using var service = new ServiceController(ServiceName);
            if (service.Status == ServiceControllerStatus.Running)
            {
                return (true, "Service is already running");
            }

            if (service.Status == ServiceControllerStatus.StartPending)
            {
                return (true, "Service is starting");
            }

            service.Start();
            await WaitForStatusAsync(service, ServiceControllerStatus.Running);
            return (true, "Service started successfully");
        }
        catch (InvalidOperationException)
        {
            return (false, "Service not found");
        }
        catch (Exception e)
        {
            return (false, $"Failed to start service: {e.Message}");
        }
    }

    public async Task<(bool success, string message)> StopServiceAsync()
    {
        try
        {
            using var service = new ServiceController(ServiceName);
            if (service.Status == ServiceControllerStatus.Stopped)
            {
                return (true, "Service is already stopped");
            }

            if (service.Status == ServiceControllerStatus.StopPending)
            {
                return (true, "Service is stopping");
            }

            service.Stop();
            await WaitForStatusAsync(service, ServiceControllerStatus.Stopped);
            return (true, "Service stopped successfully");
        }
        catch (InvalidOperationException)
        {
            return (false, "Service not found");
        }
        catch (Exception e)
        {
            return (false, $"Failed to stop service: {e.Message}");
        }
    }

    public async Task<ServiceControllerStatus?> GetServiceStatusAsync()
    {
        try
        {
            using var service = new ServiceController(ServiceName);
            await Task.Run(() => service.Refresh());
            return service.Status;
        }
        catch (Exception e)
        {
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