using FastTools.App.Infrastructure;
using FastTools.App.Models;
using FastTools.App.Services;
using System.Diagnostics;

namespace FastTools.App.Providers;

public sealed class FileSearchProvider : ISearchProvider
{
    private readonly FileIndexService _indexService;

    public FileSearchProvider(FileIndexService indexService)
    {
        _indexService = indexService;
    }

    public Task<IReadOnlyList<SearchResultItem>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var normalizedQuery = SearchMatcher.Normalize(query);
        if (normalizedQuery.Length < 2)
        {
            return Task.FromResult<IReadOnlyList<SearchResultItem>>([]);
        }

        _ = _indexService.EnsureBackgroundRefreshAsync();
        var entries = _indexService.GetSnapshot();
        var matches = new List<(FileIndexEntry Entry, double Score)>(12);

        for (var index = 0; index < entries.Count; index++)
        {
            if ((index & 127) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var entry = entries[index];
            var score = SearchMatcher.ScoreNormalized(
                normalizedQuery,
                entry.NormalizedName,
                entry.NormalizedParentDirectory);
            if (score <= 0)
            {
                continue;
            }

            InsertTopMatch(matches, entry, score, 12);
        }

        var results = matches
            .Select(match => BuildResult(match.Entry, match.Score))
            .ToList();

        return Task.FromResult<IReadOnlyList<SearchResultItem>>(results);
    }

    private static SearchResultItem BuildResult(FileIndexEntry entry, double score)
    {
        return new SearchResultItem
        {
            Key = $"file:{entry.FullPath}",
            Title = entry.Name,
            Subtitle = entry.ParentDirectory,
            Group = "Files",
            Glyph = entry.IsDirectory ? "\uE838" : "\uE8A5",
            Score = score,
            ExecuteAsync = _ =>
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = entry.FullPath,
                    UseShellExecute = true,
                });

                return Task.CompletedTask;
            },
            Actions =
            [
                new SearchAction
                {
                    Label = "Open Folder",
                    Shortcut = string.Empty,
                    ExecuteAsync = () =>
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"/select,\"{entry.FullPath}\"",
                            UseShellExecute = true,
                        });
                        return Task.CompletedTask;
                    },
                },
                new SearchAction
                {
                    Label = "Copy Path",
                    Shortcut = "Ctrl+C",
                    ExecuteAsync = () =>
                    {
                        System.Windows.Clipboard.SetText(entry.FullPath);
                        return Task.CompletedTask;
                    },
                },
            ],
        };
    }

    private static void InsertTopMatch(
        List<(FileIndexEntry Entry, double Score)> matches,
        FileIndexEntry entry,
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
