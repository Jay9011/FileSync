using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using System.Windows.Threading;
using S1FileSync.Services.Interface;

namespace S1FileSync.Services;

/// <summary>
/// 트레이아이콘 상태
/// </summary>
public enum TrayIconStatus
{
    Normal,
    Syncing,
    Stop,
    Error
}

/// <summary>
/// 트레이아이콘 색상
/// </summary>
public enum IconColor
{
    Normal,
    Red,
    Green
}

public class TrayIconService : ITrayIconService, IDisposable
{
    private NotifyIcon? _notifyIcon;
    private Icon? _originalIcon;
    private readonly DispatcherTimer _blinkTimer;
    private bool _isBlinking;
    private TrayIconStatus _currentStatus = TrayIconStatus.Normal;
    private readonly IconColor _normalColor = IconColor.Normal;
    private IconColor _currentColor = IconColor.Normal;
    
    private readonly Dictionary<IconColor, Icon> _iconCache = new Dictionary<IconColor, Icon>();

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
            PreGenerateIcons();
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

    /// <summary>
    /// 상태 변경
    /// </summary>
    /// <param name="status"></param>
    public void SetStatus(TrayIconStatus status)
    {
        if (_currentStatus == status)
        {
            return;
        }

        switch (status)
        {
            case TrayIconStatus.Normal:
                break;
            case TrayIconStatus.Syncing:
                break;
            case TrayIconStatus.Stop:
                break;
            case TrayIconStatus.Error:
                if (_currentStatus == TrayIconStatus.Syncing)
                {
                    return;
                }
                break;
        }
        
        _currentStatus = status;

        switch (status)
        {
            case TrayIconStatus.Normal:
                UpdateIcon(IconColor.Normal, false);
                _notifyIcon!.Text = "S1FileSync - Ready";
                break;
            case TrayIconStatus.Syncing:
                UpdateIcon(IconColor.Green, true);
                _notifyIcon!.Text = "S1FileSync - Syncronizing...";
                break;
            case TrayIconStatus.Stop:
                UpdateIcon(IconColor.Red, false);
                _notifyIcon!.Text = "S1FileSync - Stopped";
                break;
            case TrayIconStatus.Error:
                UpdateIcon(IconColor.Red, false);
                _notifyIcon!.Text = "S1FileSync - Error";
                break;
        }
    }

    public TrayIconStatus GetStatus()
    {
        return _currentStatus;
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
        _originalIcon?.Dispose();
        _blinkTimer?.Stop();
    }
    
    /// <summary>
    /// 각 색상 아이콘 생성
    /// </summary>
    private void PreGenerateIcons()
    {
        if (_originalIcon == null)
        {
            return;
        }

        _iconCache[IconColor.Normal] = (Icon)_originalIcon.Clone();

        using var bitmap = new Bitmap(_originalIcon.ToBitmap());

        foreach (IconColor color in Enum.GetValues<IconColor>())
        {
            if (color == IconColor.Normal)
            {
                continue;
            }

            using var coloredBitmap = new Bitmap(bitmap.Width, bitmap.Height);
            using var graphics = Graphics.FromImage(coloredBitmap);
            
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    Color pixelColor = bitmap.GetPixel(x, y);
                    Color newColor = pixelColor;

                    if (pixelColor.A == 0)
                    {
                        
                    } 
                    else if (pixelColor.R > 240 && pixelColor.G > 240 && pixelColor.B > 240)
                    {
                        
                    }
                    else 
                    {
                        int maxChannel = Math.Max(pixelColor.R, Math.Max(pixelColor.G, pixelColor.B));

                        newColor = color switch
                        {
                            IconColor.Red => Color.FromArgb(
                                pixelColor.A,
                                Math.Min(255, (int)(maxChannel * 2.0)),
                                Math.Min(255, (int)(maxChannel * 0.5)),
                                Math.Min(255, (int)(maxChannel * 0.5))
                            ),
                            IconColor.Green => Color.FromArgb(
                                pixelColor.A,
                                27,
                                140,
                                60
                            ),
                            _ => pixelColor
                        };
                    }
                
                    coloredBitmap.SetPixel(x, y, newColor);
                }
            }
            
            _iconCache[color] = Icon.FromHandle(coloredBitmap.GetHicon());
        }
    }

    /// <summary>
    /// 아이콘 업데이트
    /// </summary>
    /// <param name="color">아이콘 색상</param>
    /// <param name="enableBlinking">깜빡거림</param>
    private void UpdateIcon(IconColor color, bool enableBlinking)
    {
        if (_notifyIcon == null) return;
        
        _currentColor = color;
        
        if (enableBlinking)
        {
            StartBlinking();
        }
        else
        {
            StopBlinking();
            SetIconColor(color);
        }
    }

    /// <summary>
    /// 아이콘 색상 변경
    /// </summary>
    /// <param name="color"></param>
    private void SetIconColor(IconColor color)
    {
        if (_notifyIcon == null || !_iconCache.ContainsKey(color))
        {
            return;
        }
        
        _notifyIcon.Icon = _iconCache[color];
    }
    
    /// <summary>
    /// 아이콘 깜빡임 시작
    /// </summary>
    private void StartBlinking()
    {
        if (!_blinkTimer.IsEnabled)
        {
            _isBlinking = false;
            _blinkTimer.Start();
        }
    }
    
    /// <summary>
    /// 아이콘 깜빡임 중지
    /// </summary>
    private void StopBlinking()
    {
        _blinkTimer.Stop();
        _isBlinking = false;
    }
    
    /// <summary>
    /// 아이콘 깜빡임 타이머
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void BlinkTimer_Tick(object? sender, EventArgs e)
    {
        _isBlinking = !_isBlinking;
        SetIconColor(_isBlinking ? _currentColor : _normalColor);
    }

}