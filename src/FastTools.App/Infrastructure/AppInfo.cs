using System.Diagnostics;
using System.Reflection;

namespace FastTools.App.Infrastructure;

public static class AppInfo
{
    public const string GitHubUrl = "https://github.com/ban-yin/fastTools";

    public const string AuthorName = "ban-yin";

    private static string? _version;

    public static string Version => _version ??= ResolveVersion();

    public static string Copyright => $"© {DateTime.Now.Year} {AuthorName}";

    private static string ResolveVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoVersion))
        {
            // Strip git metadata after '+' if present.
            var plus = infoVersion.IndexOf('+');
            return plus >= 0 ? infoVersion[..plus] : infoVersion;
        }

        var location = assembly.Location;
        if (!string.IsNullOrEmpty(location))
        {
            var fileVersion = FileVersionInfo.GetVersionInfo(location).FileVersion;
            if (!string.IsNullOrWhiteSpace(fileVersion))
            {
                return fileVersion!;
            }
        }

        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }
}
