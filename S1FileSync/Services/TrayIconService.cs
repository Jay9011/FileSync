using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using System.Windows.Threading;
using S1FileSync.Services.Interface;

namespace S1FileSync.Services;

public enum TrayIconStatus
{
    Normal,
    Syncing,
    Error
}

public class TrayIconService : ITrayIconService, IDisposable
{
    private NotifyIcon? _notifyIcon;
    private Icon? _originalIcon;
    private readonly DispatcherTimer _blinkTimer;
    private bool _isBlinking;
    private TrayIconStatus _currentStatus = TrayIconStatus.Normal;

    public event EventHandler? WindowOpenRequested;
    public event EventHandler? ShutdownRequested;

    public TrayIconService()
    {
        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _blinkTimer.Tick += BlinkTimer_Tick;
    }
    
    public void Initialize()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(iconPath))
        {
            _originalIcon = new Icon(iconPath);
        }
        
        _notifyIcon = new NotifyIcon
        {
            Icon = _originalIcon ?? SystemIcons.Application,
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
    
    public void SetStatus(TrayIconStatus status)
    {
        _currentStatus = status;

        switch (status)
        {
            case TrayIconStatus.Normal:
                StopBlinking();
                UpdateIcon(false);
                _notifyIcon!.Text = "S1FileSync - Ready";
                break;
            case TrayIconStatus.Syncing:
                StartBlinking();
                _notifyIcon!.Text = "S1FileSync - Syncronizing...";
                break;
            case TrayIconStatus.Error:
                StopBlinking();
                UpdateIcon(true, true);
                _notifyIcon!.Text = "S1FileSync - Error";
                break;
        }
    }
    
    public void Dispose()
    {
        _notifyIcon?.Dispose();
        _originalIcon?.Dispose();
    }
    
    private void StartBlinking()
    {
        if (!_blinkTimer.IsEnabled)
        {
            _isBlinking = false;
            _blinkTimer.Start();
        }
    }
    
    private void StopBlinking()
    {
        _blinkTimer.Stop();
        UpdateIcon(false);
    }
    
    private void BlinkTimer_Tick(object? sender, EventArgs e)
    {
        _isBlinking = !_isBlinking;
        UpdateIcon(_isBlinking);
    }

    private void UpdateIcon(bool highlight, bool isError = false)
    {
        if (_originalIcon == null || _notifyIcon == null)
        {
            return;
        }

        using (var bitmap = new Bitmap(_originalIcon.ToBitmap()))
        {
            if (highlight || isError)
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    var colorMatrix = new ColorMatrix();

                    if (isError)
                    {
                        colorMatrix.Matrix00 = 1.5f; // R
                        colorMatrix.Matrix11 = 0.5f; // G
                        colorMatrix.Matrix22 = 0.5f; // B
                    }
                    else
                    {
                        colorMatrix.Matrix00 = 0.5f; // R
                        colorMatrix.Matrix11 = 1.5f; // G
                        colorMatrix.Matrix22 = 0.5f; // B
                    }
                    
                    var imageAttributes = new ImageAttributes();
                    imageAttributes.SetColorMatrix(colorMatrix);
                    
                    graphics.DrawImage(bitmap,
                        new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                        0, 0, bitmap.Width, bitmap.Height,
                        GraphicsUnit.Pixel,
                        imageAttributes);
                }
            }
            
            using (var icon = Icon.FromHandle(bitmap.GetHicon()))
            {
                _notifyIcon.Icon = icon;
            }
        }
    }
}