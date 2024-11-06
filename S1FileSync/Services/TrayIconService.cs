using System.Drawing;
using System.Windows.Forms;
using S1FileSync.Services.Interface;

namespace S1FileSync.Services;

public class TrayIconService : ITrayIconService, IDisposable
{
    private NotifyIcon? _notifyIcon;

    public event EventHandler? WindowOpenRequested;
    public event EventHandler? ShutdownRequested;

    public void Initialize()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "S1FileSync"
        };

        var contextMenu = new ContextMenuStrip();
        
        var openMenuItem = new ToolStripMenuItem("Open");
        openMenuItem.Click += (sender, args) => WindowOpenRequested?.Invoke(sender, args);
        
        var exitMenuItem = new ToolStripMenuItem("Exit");
        exitMenuItem.Click += (sender, args) => ShutdownRequested?.Invoke(sender, args);

        contextMenu.Items.Add(openMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitMenuItem);

        // 컨텍스트 메뉴 설정
        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (sender, args) => WindowOpenRequested?.Invoke(sender, args);
    }
    
    public void Dispose()
    {
        _notifyIcon?.Dispose();
    }
}