using FileIOHelper;
using FileIOHelper.Helpers;
using NetConnectionHelper;
using NetConnectionHelper.Interface;
using S1FileSync.Services;
using S1FileSync.Services.Interface;
using S1FileSyncService;
using S1FileSyncService.Services;
using S1FileSyncService.Services.Interfaces;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<FileSyncWorker>();

// Add services
builder.Services.AddSingleton<ISettingsService, SettingsService>();
builder.Services.AddSingleton<IFileSync, FileSyncService>();
builder.Services.AddSingleton<IRemoteConnectionHelper, RemoteConnectionSmbHelper>();
builder.Services.AddSingleton<IIniFileHelper>(sp => new IniFileHelper(Path.Combine(Directory.GetCurrentDirectory(), "settings.ini")));

builder.Services.AddWindowsService();

var host = builder.Build();
host.Run();
