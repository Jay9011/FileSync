using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using FileIOHelper;
using FileIOHelper.Helpers;
using Microsoft.Extensions.Hosting.WindowsServices;
using NetConnectionHelper;
using NetConnectionHelper.Interface;
using S1FileSync.Services;
using S1FileSync.Services.Interface;
using S1FileSyncService;
using S1FileSyncService.Services;
using S1FileSyncService.Services.Interfaces;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default,
    ApplicationName = AppDomain.CurrentDomain.FriendlyName,
});

#if DEBUG
if (args.Contains("--console"))
{
    builder.Services.AddHostedService<FileSyncWorker>();
}
else
{
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "S1FileSyncService";
    });
    builder.Services.AddHostedService<FileSyncWorker>();
}
#else

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "S1FileSyncService";
});

builder.Services.AddHostedService<FileSyncWorker>();

#endif

// Add services
builder.Services.AddSingleton<ISettingsService, SettingsService>();
builder.Services.AddSingleton<IFileSync, FileSyncService>();
builder.Services.AddSingleton<IRemoteConnectionHelper, RemoteConnectionSmbHelper>();
builder.Services.AddSingleton<FileSyncIPCServer>();
builder.Services.AddSingleton<ISyncProgressWithUI, SyncProgressUI>();
builder.Services.AddSingleton<ISendMessage, SyncSendMessage>();

string iniFilePath = Path.Combine(WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : Directory.GetCurrentDirectory(), "settings.ini");
builder.Services.AddSingleton<IIniFileHelper>(sp => new IniFileHelper(iniFilePath));

builder.Logging.AddEventLog(settings =>
{
    settings.SourceName = "S1FileSyncService";
    settings.LogName = "Application";
});

try
{
    var host = builder.Build();

    if (WindowsServiceHelpers.IsWindowsService())
    {
        string baseDir = AppContext.BaseDirectory;
        string logsDir = Path.Combine(baseDir, "logs");

        if (!Directory.Exists(logsDir))
        {
            Directory.CreateDirectory(logsDir);
        }

        if (!File.Exists(iniFilePath))
        {
            File.WriteAllText(iniFilePath, string.Empty);
        }

        try
        {
            SecurityIdentifier serviceSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

            DirectoryInfo dirInfo = new DirectoryInfo(baseDir);
            DirectorySecurity dirSecurity = dirInfo.GetAccessControl();

            dirSecurity.AddAccessRule(new FileSystemAccessRule(
                serviceSid,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            dirInfo.SetAccessControl(dirSecurity);

            DirectoryInfo logsDirInfo = new DirectoryInfo(logsDir);
            DirectorySecurity logsDirSecurity = logsDirInfo.GetAccessControl();

            logsDirSecurity.AddAccessRule(new FileSystemAccessRule(
                serviceSid,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            logsDirInfo.SetAccessControl(logsDirSecurity);

            FileInfo iniFileInfo = new FileInfo(iniFilePath);
            FileSecurity iniFileSecurity = iniFileInfo.GetAccessControl();

            iniFileSecurity.AddAccessRule(new FileSystemAccessRule(
                serviceSid,
                FileSystemRights.FullControl,
                AccessControlType.Allow));

            iniFileInfo.SetAccessControl(iniFileSecurity);
        }
        catch (Exception e)
        {
            var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("Program");
            logger.LogError(e, "Error setting permissions");
        }
    }

    await host.RunAsync();
}
catch (Exception ex)
{
    if (!EventLog.SourceExists("S1FileSyncService"))
    {
        EventLog.CreateEventSource("S1FileSyncService", "Application");
    }
    EventLog.WriteEntry("S1FileSyncService", $"Service failed to start: {ex.Message} \n {ex.StackTrace}", EventLogEntryType.Error);
    throw;
}
