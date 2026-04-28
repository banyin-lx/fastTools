using FastTools.App.Models;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FastTools.App.Infrastructure;

public static class AppIconLoader
{
    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? Load(ApplicationEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.LaunchTarget))
        {
            return null;
        }

        if (entry.IsPackagedApp)
        {
            var key = $"appsfolder:{entry.LaunchTarget}";
            return Cache.GetOrAdd(key, _ => LoadShellIcon($"shell:AppsFolder\\{entry.LaunchTarget}"));
        }

        return Cache.GetOrAdd(entry.LaunchTarget, static path => LoadCore(path));
    }

    public static ImageSource? LoadFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Cache.GetOrAdd(path, static p => LoadCore(p));
    }

    private static ImageSource? LoadCore(string path)
    {
        try
        {
            var iconSourcePath = ResolveIconSourcePath(path);
            if (string.IsNullOrWhiteSpace(iconSourcePath) || !File.Exists(iconSourcePath))
            {
                return null;
            }

            using var icon = Icon.ExtractAssociatedIcon(iconSourcePath);
            if (icon is null)
            {
                return null;
            }

            var source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(20, 20));
            source.Freeze();
            return source;
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveIconSourcePath(string path)
    {
        if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        object? shell = null;
        object? shortcut = null;

        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return path;
            }

            shell = Activator.CreateInstance(shellType);
            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                [path]);

            var targetPath = shortcut?.GetType().InvokeMember(
                "TargetPath",
                BindingFlags.GetProperty,
                null,
                shortcut,
                null) as string;

            return !string.IsNullOrWhiteSpace(targetPath) && File.Exists(targetPath)
                ? targetPath
                : path;
        }
        catch
        {
            return path;
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut))
            {
                Marshal.ReleaseComObject(shortcut);
            }

            if (shell is not null && Marshal.IsComObject(shell))
            {
                Marshal.ReleaseComObject(shell);
            }
        }
    }

    private static ImageSource? LoadShellIcon(string parsingName)
    {
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            SHCreateItemFromParsingName(parsingName, IntPtr.Zero, iid, out var factory);
            if (factory is null)
            {
                return null;
            }

            try
            {
                var size = new SIZE { cx = 32, cy = 32 };
                var hr = factory.GetImage(size, SIIGBF.IconOnly | SIIGBF.BiggerSizeOk, out var hbitmap);
                if (hr != 0 || hbitmap == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    var source = Imaging.CreateBitmapSourceFromHBitmap(
                        hbitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(20, 20));
                    source.Freeze();
                    return source;
                }
                finally
                {
                    DeleteObject(hbitmap);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(factory);
            }
        }
        catch
        {
            return null;
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [ComImport]
    [Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage([In] SIZE size, [In] SIIGBF flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    [Flags]
    private enum SIIGBF
    {
        ResizeToFit = 0x00,
        BiggerSizeOk = 0x01,
        MemoryOnly = 0x02,
        IconOnly = 0x04,
        ThumbnailOnly = 0x08,
        InCacheOnly = 0x10,
    }
}
