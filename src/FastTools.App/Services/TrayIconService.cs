using FastTools.App.Views;
using System.Runtime.InteropServices;
using System.Windows;
using Forms = System.Windows.Forms;

namespace FastTools.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Icon _trayIcon;
    private LocalizationService? _localizer;
    private TrayMenuWindow? _menuWindow;
    private System.Windows.Point _lastTrayClickPosition;

    public TrayIconService()
    {
        _trayIcon = LoadTrayIcon() ?? SystemIcons.Application;

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "FastTools",
            Visible = true,
            Icon = _trayIcon,
        };

        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        _notifyIcon.MouseUp += NotifyIcon_MouseUp;
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? RefreshRequested;

    public event EventHandler? ExitRequested;

    public void ApplyLocalization(LocalizationService localizer)
    {
        _localizer = localizer;
    }

    public void ShowBalloonTip(string title, string text, Forms.ToolTipIcon icon = Forms.ToolTipIcon.Info)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.BalloonTipIcon = icon;
        _notifyIcon.ShowBalloonTip(3500);
    }

    public void Dispose()
    {
        _menuWindow?.Close();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        if (!ReferenceEquals(_trayIcon, SystemIcons.Application))
        {
            _trayIcon.Dispose();
        }
    }

    private void NotifyIcon_MouseUp(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button != Forms.MouseButtons.Right)
        {
            return;
        }

        _lastTrayClickPosition = new System.Windows.Point(Forms.Cursor.Position.X, Forms.Cursor.Position.Y);
        var application = System.Windows.Application.Current;
        application.Dispatcher.Invoke(ShowTrayMenu);
    }

    private void ShowTrayMenu()
    {
        if (_menuWindow is not null)
        {
            _menuWindow.Close();
            _menuWindow = null;
            return;
        }

        var localizer = _localizer ?? (LocalizationService)System.Windows.Application.Current.Resources["Loc"];
        _menuWindow = new TrayMenuWindow(
            localizer,
            () => OpenRequested?.Invoke(this, EventArgs.Empty),
            () => SettingsRequested?.Invoke(this, EventArgs.Empty),
            () => RefreshRequested?.Invoke(this, EventArgs.Empty),
            () => ExitRequested?.Invoke(this, EventArgs.Empty));

        _menuWindow.Closed += (_, _) => _menuWindow = null;
        _menuWindow.Loaded += TrayMenuWindow_Loaded;
        _menuWindow.Show();
        _menuWindow.Activate();
    }

    private void TrayMenuWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not TrayMenuWindow window)
        {
            return;
        }

        var anchor = _lastTrayClickPosition;
        var screenBounds = Forms.Screen.FromPoint(new System.Drawing.Point((int)anchor.X, (int)anchor.Y)).Bounds;
        var left = anchor.X - (window.ActualWidth / 2d);
        var top = anchor.Y - window.ActualHeight + 50;

        window.Left = Math.Clamp(left, screenBounds.Left + 8, screenBounds.Right - window.ActualWidth - 8);
        window.Top = Math.Clamp(top, screenBounds.Top + 8, screenBounds.Bottom - window.ActualHeight - 8);
    }

    private static Icon? LoadTrayIcon()
    {
        try
        {
            var iconUri = new Uri("pack://application:,,,/Assets/fastTools.png", UriKind.Absolute);
            var resourceStream = System.Windows.Application.GetResourceStream(iconUri);
            if (resourceStream?.Stream is null)
            {
                return null;
            }

            using var stream = resourceStream.Stream;
            using var bitmap = new Bitmap(stream);
            var hIcon = bitmap.GetHicon();
            try
            {
                return (Icon)Icon.FromHandle(hIcon).Clone();
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }
        catch
        {
            return null;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
