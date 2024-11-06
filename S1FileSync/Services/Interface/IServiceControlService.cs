using System.ServiceProcess;

namespace S1FileSync.Services.Interface;

public interface IServiceControlService
{
    /// <summary>
    /// 서비스 시작
    /// </summary>
    /// <returns></returns>
    Task<(bool success, string message)> StartServiceAsync();
    /// <summary>
    /// 서비스 중지
    /// </summary>
    /// <returns></returns>
    Task<(bool success, string message)> StopServiceAsync();
    /// <summary>
    /// 현재 서비스 상태 조회
    /// </summary>
    /// <returns></returns>
    Task<ServiceControllerStatus?> GetServiceStatusAsync();
}