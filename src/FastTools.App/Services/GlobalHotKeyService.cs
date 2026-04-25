using FastTools.App.Models;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace FastTools.App.Services;

public sealed class GlobalHotKeyService : IDisposable
{
    private const int HotKeyId = 0xB101;

    private IntPtr _windowHandle;
    private bool _registered;

    public event EventHandler? HotKeyPressed;

    public bool Register(IntPtr windowHandle, string gestureText, out string? errorMessage)
    {
        errorMessage = null;
        _windowHandle = windowHandle;
        Unregister();

        if (!HotKeyGesture.TryParse(gestureText, out var gesture) || gesture is null)
        {
            errorMessage = "快捷键格式无效，请使用如 Alt+Space 的格式。";
            return false;
        }

        var virtualKey = KeyInterop.VirtualKeyFromKey(gesture.Key);
        _registered = RegisterHotKey(windowHandle, HotKeyId, (uint)gesture.Modifiers, (uint)virtualKey);
        if (_registered)
        {
            return true;
        }

        errorMessage = "全局快捷键注册失败，可能已被其他程序占用。";
        return false;
    }

    public bool HandleMessage(int message, IntPtr wParam)
    {
        if (message != NativeMethods.WmHotKey || wParam.ToInt32() != HotKeyId)
        {
            return false;
        }

        HotKeyPressed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Unregister()
    {
        if (!_registered || _windowHandle == IntPtr.Zero)
        {
            return;
        }

        UnregisterHotKey(_windowHandle, HotKeyId);
        _registered = false;
    }

    public void Dispose()
    {
        Unregister();
        GC.SuppressFinalize(this);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
