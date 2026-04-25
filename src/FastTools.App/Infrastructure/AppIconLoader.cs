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
        if (entry.IsPackagedApp)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(entry.LaunchTarget))
        {
            return null;
        }

        return Cache.GetOrAdd(entry.LaunchTarget, static path => LoadCore(path));
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
}
