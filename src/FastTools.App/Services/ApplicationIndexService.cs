using FastTools.App.Infrastructure;
using FastTools.App.Models;
using System.Diagnostics;
using System.Text.Json;

namespace FastTools.App.Services;

public sealed class ApplicationIndexService
{
    private readonly LauncherSettingsStore _settingsStore;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private IReadOnlyList<ApplicationEntry> _cache = [];
    private Task? _backgroundRefreshTask;
    private bool _loaded;

    public ApplicationIndexService(LauncherSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        _settingsStore.SettingsChanged += (_, _) => _loaded = false;
    }

    public bool IsLoaded => _loaded;

    public IReadOnlyList<ApplicationEntry> GetSnapshot()
    {
        return _cache;
    }

    public Task EnsureBackgroundRefreshAsync()
    {
        if (_loaded)
        {
            return Task.CompletedTask;
        }

        if (_backgroundRefreshTask is { IsCompleted: false })
        {
            return _backgroundRefreshTask;
        }

        _backgroundRefreshTask = RefreshAsync();
        return _backgroundRefreshTask;
    }

    public async Task RefreshAsync()
    {
        await _refreshGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var entries = await Task.Run(() =>
            {
                var batch = new List<ApplicationEntry>();
                var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var directory in GetApplicationDirectories())
                {
                    if (!Directory.Exists(directory))
                    {
                        continue;
                    }

                    IEnumerable<string> files;
                    try
                    {
                        files = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
                            .Where(path =>
                            {
                                var extension = Path.GetExtension(path);
                                return extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
                                    || extension.Equals(".exe", StringComparison.OrdinalIgnoreCase);
                            });
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var file in files)
                    {
                        if (!seenFiles.Add(file))
                        {
                            continue;
                        }

                        batch.Add(new ApplicationEntry
                        {
                            Key = $"app:{file}",
                            Name = Path.GetFileNameWithoutExtension(file),
                            LaunchTarget = file,
                            Location = file,
                            NormalizedName = SearchMatcher.Normalize(Path.GetFileNameWithoutExtension(file)),
                            NormalizedLocation = SearchMatcher.Normalize(file),
                            NormalizedAliases = string.Empty,
                        });
                    }
                }

                return batch;
            }).ConfigureAwait(false);

            var seen = new HashSet<string>(
                entries.Select(entry => entry.Key),
                StringComparer.OrdinalIgnoreCase);

            foreach (var packagedApp in await LoadPackagedAppsAsync().ConfigureAwait(false))
            {
                if (!seen.Add(packagedApp.Key))
                {
                    continue;
                }

                entries.Add(packagedApp);
            }

            _cache = entries
                .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            _loaded = true;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private IEnumerable<string> GetApplicationDirectories()
    {
        var directories = new List<string>
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                "Programs"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "Programs"),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
        };

        directories.AddRange(_settingsStore.Current.ApplicationDirectories);

        return directories
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<IReadOnlyList<ApplicationEntry>> LoadPackagedAppsAsync()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -Command \"Get-StartApps | Select-Object Name,AppID | ConvertTo-Json -Compress\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return [];
            }

            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(output))
            {
                return [];
            }

            var document = JsonDocument.Parse(output);
            var entries = new List<ApplicationEntry>();

            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in document.RootElement.EnumerateArray())
                {
                    TryAddPackagedApp(entries, element);
                }
            }
            else if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                TryAddPackagedApp(entries, document.RootElement);
            }

            return entries;
        }
        catch
        {
            return [];
        }
    }

    private static void TryAddPackagedApp(List<ApplicationEntry> entries, JsonElement element)
    {
        if (!element.TryGetProperty("Name", out var nameProperty) ||
            !element.TryGetProperty("AppID", out var appIdProperty))
        {
            return;
        }

        var name = nameProperty.GetString();
        var appId = appIdProperty.GetString();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(appId))
        {
            return;
        }

        entries.Add(new ApplicationEntry
        {
            Key = $"app:{appId}",
            Name = name,
            LaunchTarget = appId,
            Location = "Packaged app",
            IsPackagedApp = true,
            NormalizedName = SearchMatcher.Normalize(name),
            NormalizedLocation = SearchMatcher.Normalize("Packaged app"),
            NormalizedAliases = string.Empty,
        });
    }
}
