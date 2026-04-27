using FastTools.Plugin.Abstractions.Contracts;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FastTools.Plugin.FolderIndex.Plugins;

public sealed class FolderIndexPlugin : ILauncherPlugin
{
    private const string DirectoriesSettingKey = "index_directories_json";
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private IReadOnlyList<IndexedEntry> _cache = [];
    private Task? _backgroundRefreshTask;
    private string _loadedSignature = string.Empty;
    private bool _loaded;

    public string Id => "fasttools.folderindex";

    public string DisplayName => "Folder Index";

    public string Description => "扫描配置目录并建立本地索引，支持离线文件和文件夹搜索。";

    public PluginConfiguration GetConfiguration()
    {
        return new PluginConfiguration(
        [
            new PluginDirectoryListSettingDefinition(
                DirectoriesSettingKey,
                "索引目录",
                "仅在这些目录中建立索引。目录越多，初次扫描越久。"),
        ]);
    }

    public Task<IReadOnlyList<PluginSearchItem>> QueryAsync(PluginQuery query, CancellationToken cancellationToken)
    {
        var trimmed = query.Text.Trim();
        var normalizedQuery = Normalize(trimmed);
        if (normalizedQuery.Length < 2)
        {
            return Task.FromResult<IReadOnlyList<PluginSearchItem>>([]);
        }

        var directories = ResolveDirectories(query.Settings);
        _ = EnsureBackgroundRefreshAsync(directories);
        var snapshot = _cache;

        var matches = new List<(IndexedEntry Entry, double Score)>(12);
        for (var i = 0; i < snapshot.Count; i++)
        {
            if ((i & 127) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var entry = snapshot[i];
            var score = ScoreNormalized(normalizedQuery, entry.NormalizedName, entry.NormalizedParentDirectory);
            if (score <= 0)
            {
                continue;
            }

            InsertTopMatch(matches, entry, score, 12);
        }

        var results = matches
            .Select(match => new PluginSearchItem(
                $"folderindex:{match.Entry.FullPath}",
                match.Entry.Name,
                match.Entry.ParentDirectory,
                "Files",
                match.Entry.IsDirectory ? "\uE838" : "\uE8A5",
                match.Score))
            .ToList();

        return Task.FromResult<IReadOnlyList<PluginSearchItem>>(results);
    }

    public Task ExecuteAsync(PluginExecutionRequest request, CancellationToken cancellationToken)
    {
        var fullPath = request.Item.Key["folderindex:".Length..];
        Process.Start(new ProcessStartInfo
        {
            FileName = fullPath,
            UseShellExecute = true,
        });

        return Task.CompletedTask;
    }

    private Task EnsureBackgroundRefreshAsync(IReadOnlyList<string> directories)
    {
        var signature = BuildSignature(directories);
        if (_loaded && string.Equals(signature, _loadedSignature, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        if (_backgroundRefreshTask is { IsCompleted: false })
        {
            return _backgroundRefreshTask;
        }

        _backgroundRefreshTask = RefreshAsync(directories, signature);
        return _backgroundRefreshTask;
    }

    private async Task RefreshAsync(IReadOnlyList<string> directories, string signature)
    {
        await _refreshGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_loaded && string.Equals(signature, _loadedSignature, StringComparison.Ordinal))
            {
                return;
            }

            var batch = await Task.Run(() =>
            {
                var entries = new List<IndexedEntry>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var root in directories)
                {
                    EnumerateEntries(root, entries, seen);
                }

                return (IReadOnlyList<IndexedEntry>)entries;
            }).ConfigureAwait(false);

            _cache = batch;
            _loadedSignature = signature;
            _loaded = true;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private static IReadOnlyList<string> ResolveDirectories(IReadOnlyDictionary<string, string> settings)
    {
        if (settings.TryGetValue(DirectoriesSettingKey, out var directoriesJson) &&
            !string.IsNullOrWhiteSpace(directoriesJson))
        {
            try
            {
                var directories = JsonSerializer.Deserialize<List<string>>(directoriesJson);
                if (directories is { Count: > 0 })
                {
                    return directories
                        .Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
            }
            catch
            {
            }
        }

        return [];
    }

    private static string BuildSignature(IReadOnlyList<string> directories)
    {
        return string.Join("|", directories.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
    }

    private static void EnumerateEntries(string root, List<IndexedEntry> results, HashSet<string> seen)
    {
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
            {
                if (!seen.Add(directory))
                {
                    continue;
                }

                var directoryName = Path.GetFileName(directory);
                var parent = Directory.GetParent(directory)?.FullName ?? root;
                results.Add(new IndexedEntry(
                    directory,
                    directoryName,
                    parent,
                    true,
                    Normalize(directoryName),
                    Normalize(parent)));
            }

            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (!seen.Add(file))
                {
                    continue;
                }

                var fileName = Path.GetFileName(file);
                var parent = Path.GetDirectoryName(file) ?? root;
                results.Add(new IndexedEntry(
                    file,
                    fileName,
                    parent,
                    false,
                    Normalize(fileName),
                    Normalize(parent)));
            }
        }
        catch
        {
        }
    }

    private static double ScoreNormalized(string normalizedQuery, params string?[] normalizedCandidates)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return 0;
        }

        var best = 0d;
        foreach (var candidate in normalizedCandidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (candidate.Equals(normalizedQuery, StringComparison.Ordinal))
            {
                best = Math.Max(best, 200);
                continue;
            }

            if (candidate.StartsWith(normalizedQuery, StringComparison.Ordinal))
            {
                best = Math.Max(best, 160 - (candidate.Length - normalizedQuery.Length) * 0.2);
                continue;
            }

            var containsIndex = candidate.IndexOf(normalizedQuery, StringComparison.Ordinal);
            if (containsIndex >= 0)
            {
                best = Math.Max(best, 130 - containsIndex * 2);
                continue;
            }

            if (IsSubsequence(normalizedQuery, candidate, out var compactness))
            {
                best = Math.Max(best, 80 - compactness);
            }
        }

        return best;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static bool IsSubsequence(string needle, string haystack, out int compactness)
    {
        compactness = int.MaxValue;
        var firstMatch = -1;
        var lastMatch = -1;
        var index = 0;

        for (var i = 0; i < haystack.Length && index < needle.Length; i++)
        {
            if (haystack[i] != needle[index])
            {
                continue;
            }

            firstMatch = firstMatch == -1 ? i : firstMatch;
            lastMatch = i;
            index++;
        }

        if (index != needle.Length)
        {
            return false;
        }

        compactness = lastMatch - firstMatch - needle.Length;
        return true;
    }

    private static void InsertTopMatch(
        List<(IndexedEntry Entry, double Score)> matches,
        IndexedEntry entry,
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

    private sealed record IndexedEntry(
        string FullPath,
        string Name,
        string ParentDirectory,
        bool IsDirectory,
        string NormalizedName,
        string NormalizedParentDirectory);
}
