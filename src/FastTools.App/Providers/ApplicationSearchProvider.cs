using FastTools.App.Infrastructure;
using FastTools.App.Models;
using FastTools.App.Services;
using System.Diagnostics;

namespace FastTools.App.Providers;

public sealed class ApplicationSearchProvider : ISearchProvider
{
    private readonly ApplicationIndexService _indexService;
    private readonly LauncherSettingsStore _settingsStore;

    public ApplicationSearchProvider(ApplicationIndexService indexService, LauncherSettingsStore settingsStore)
    {
        _indexService = indexService;
        _settingsStore = settingsStore;
    }

    public Task<IReadOnlyList<SearchResultItem>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        _ = _indexService.EnsureBackgroundRefreshAsync();
        var entries = _indexService.GetSnapshot();
        var isBlankQuery = string.IsNullOrWhiteSpace(query);
        var normalizedQuery = SearchMatcher.Normalize(query);
        var limit = isBlankQuery ? 6 : 12;
        var matches = new List<(ApplicationEntry Entry, double Score)>(limit);
        var hideShortcuts = _settingsStore.Current.HideShortcutResults;

        for (var index = 0; index < entries.Count; index++)
        {
            if ((index & 127) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var entry = entries[index];

            if (hideShortcuts && IsShortcut(entry))
            {
                continue;
            }

            var score = isBlankQuery
                ? 12
                : SearchMatcher.ScoreNormalized(
                    normalizedQuery,
                    entry.NormalizedName,
                    entry.NormalizedLocation,
                    entry.NormalizedAliases);

            if (score <= 0)
            {
                continue;
            }

            InsertTopMatch(matches, entry, score, limit);
        }

        var results = matches
            .Select(match => BuildResult(match.Entry, match.Score))
            .ToList();

        return Task.FromResult<IReadOnlyList<SearchResultItem>>(results);
    }

    private static SearchResultItem BuildResult(ApplicationEntry entry, double score)
    {
        return new SearchResultItem
        {
            Key = entry.Key,
            Title = entry.Name,
            Subtitle = entry.Location,
            Group = "Applications",
            Glyph = "\uE7F4",
            Icon = AppIconLoader.Load(entry),
            Score = score,
            SupportsRunAsAdmin = !entry.IsPackagedApp,
            ExecuteAsync = _ => LaunchAsync(entry, false),
            ExecuteAsAdminAsync = entry.IsPackagedApp ? null : _ => LaunchAsync(entry, true),
            Actions =
            [
                new SearchAction
                {
                    Label = "Open Folder",
                    Shortcut = string.Empty,
                    ExecuteAsync = () => OpenFolderAsync(entry.Location),
                },
            ],
        };
    }

    private static Task LaunchAsync(ApplicationEntry entry, bool runAsAdmin)
    {
        if (entry.IsPackagedApp)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"shell:AppsFolder\\{entry.LaunchTarget}",
                UseShellExecute = true,
            });

            return Task.CompletedTask;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = entry.LaunchTarget,
            Arguments = entry.Arguments ?? string.Empty,
            UseShellExecute = true,
        };

        if (runAsAdmin)
        {
            startInfo.Verb = "runas";
        }

        Process.Start(startInfo);
        return Task.CompletedTask;
    }

    private static Task OpenFolderAsync(string path)
    {
        var target = File.Exists(path) ? Path.GetDirectoryName(path) ?? path : path;
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{target}\"",
            UseShellExecute = true,
        });
        return Task.CompletedTask;
    }

    private static bool IsShortcut(ApplicationEntry entry)
    {
        return !entry.IsPackagedApp
            && !string.IsNullOrEmpty(entry.LaunchTarget)
            && entry.LaunchTarget.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);
    }

    private static void InsertTopMatch(
        List<(ApplicationEntry Entry, double Score)> matches,
        ApplicationEntry entry,
        double score,
        int limit)
    {
        var insertIndex = matches.FindIndex(match => score > match.Score);
        if (insertIndex < 0)
        {
            if (matches.Count >= limit)
            {
                return;
            }

            matches.Add((entry, score));
            return;
        }

        matches.Insert(insertIndex, (entry, score));
        if (matches.Count > limit)
        {
            matches.RemoveAt(matches.Count - 1);
        }
    }
}
