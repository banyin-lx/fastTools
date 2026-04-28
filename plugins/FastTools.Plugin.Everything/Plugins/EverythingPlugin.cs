using FastTools.Plugin.Abstractions.Contracts;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FastTools.Plugin.Everything.Plugins;

public sealed class EverythingPlugin : ILauncherPlugin
{
    private const string MaxResultsSettingKey = "max_results";
    private const string DirectoriesSettingKey = "scope_directories_json";
    private const uint DefaultMaxResults = 30;

    private static readonly int[] MaxResultOptions = [10, 20, 30, 50, 100];
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public string Id => "fasttools.everything";

    public string DisplayName => "Everything Search";

    public string Description => "使用 Everything SDK 进行极速文件搜索。";

    public PluginConfiguration GetConfiguration()
    {
        return new PluginConfiguration(
        [
            new PluginSelectSettingDefinition(
                MaxResultsSettingKey,
                "最大结果数",
                [.. MaxResultOptions.Select(v => v.ToString())],
                DefaultMaxResults.ToString(),
                "每次搜索返回的最大结果数量。数量越多，搜索结果越全面，但可能略微增加耗时。"),
            new PluginDirectoryListSettingDefinition(
                DirectoriesSettingKey,
                "搜索目录范围",
                "留空则搜索全部 Everything 索引。添加目录后仅在这些目录范围内搜索。"),
        ]);
    }

    public Task<IReadOnlyList<PluginSearchItem>> QueryAsync(PluginQuery query, CancellationToken cancellationToken)
    {
        var trimmed = query.Text.Trim();
        if (trimmed.Length < 2)
        {
            return Task.FromResult<IReadOnlyList<PluginSearchItem>>([]);
        }

        var maxResults = ParseMaxResults(query.Settings);
        return QueryCoreAsync(trimmed, maxResults, query.Settings, cancellationToken);
    }

    public Task ExecuteAsync(PluginExecutionRequest request, CancellationToken cancellationToken)
    {
        var fullPath = request.Item.Key["everything:".Length..];
        Process.Start(new ProcessStartInfo
        {
            FileName = fullPath,
            UseShellExecute = true,
        });
        return Task.CompletedTask;
    }

    private static uint ParseMaxResults(IReadOnlyDictionary<string, string> settings)
    {
        if (settings.TryGetValue(MaxResultsSettingKey, out var value) &&
            uint.TryParse(value, out var parsed) &&
            parsed is >= 1 and <= 200)
        {
            return parsed;
        }

        return DefaultMaxResults;
    }

    private static async Task<IReadOnlyList<PluginSearchItem>> QueryCoreAsync(
        string query,
        uint maxResults,
        IReadOnlyDictionary<string, string> settings,
        CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!CheckAvailability())
            {
                return [];
            }

            var searchString = BuildSearchString(query, settings);
            Debug.WriteLine($"[EverythingPlugin] Query: \"{searchString}\" max={maxResults}");
            EverythingInterop.Everything_SetSearchW(searchString);
            EverythingInterop.Everything_SetMax(maxResults);
            EverythingInterop.Everything_SetOffset(0);
            EverythingInterop.Everything_SetRequestFlags(
                EverythingInterop.EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME);
            EverythingInterop.Everything_SetSort(EverythingInterop.EVERYTHING_SORT_NAME_ASCENDING);

            if (!EverythingInterop.Everything_QueryW(true))
            {
                var error = EverythingInterop.Everything_GetLastError();
                Debug.WriteLine($"[EverythingPlugin] Query failed, error code: {error}");
                return [];
            }

            cancellationToken.ThrowIfCancellationRequested();

            var count = EverythingInterop.Everything_GetNumResults();
            Debug.WriteLine($"[EverythingPlugin] Everything returned {count} result(s).");
            if (count == 0)
            {
                return [];
            }

            var actualMax = Math.Min(count, maxResults);
            var results = new List<PluginSearchItem>((int)actualMax);
            var buffer = new StringBuilder(260);
            var normalizedQuery = Normalize(query);

            for (uint i = 0; i < actualMax; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                buffer.Clear();
                EverythingInterop.Everything_GetResultFullPathNameW(i, buffer, 260);
                var fullPath = buffer.ToString();
                if (string.IsNullOrEmpty(fullPath))
                {
                    continue;
                }

                var name = Path.GetFileName(fullPath);
                var isFolder = EverythingInterop.Everything_IsResultFolder(i);
                var parent = Path.GetDirectoryName(fullPath) ?? string.Empty;
                var score = ScoreMatch(query, normalizedQuery, name);

                results.Add(new PluginSearchItem(
                    $"everything:{fullPath}",
                    name,
                    parent,
                    "Files",
                    isFolder ? "\uE838" : "\uE8A5",
                    score,
                    IconPath: fullPath));
            }

            return results;
        }
        catch (DllNotFoundException ex)
        {
            Debug.WriteLine($"[EverythingPlugin] DLL not found: {ex.Message}");
            return [];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EverythingPlugin] Search error: {ex.Message}");
            return [];
        }
        finally
        {
            Gate.Release();
        }
    }

    private static bool CheckAvailability()
    {
        try
        {
            // Calls into Everything via IPC; sets last error to EVERYTHING_ERROR_IPC if not running.
            EverythingInterop.Everything_GetMajorVersion();
            var lastError = EverythingInterop.Everything_GetLastError();
            if (lastError == EverythingInterop.EVERYTHING_ERROR_IPC)
            {
                Debug.WriteLine("[EverythingPlugin] IPC error — make sure Everything is running and 'Tools > Options > General > Enable IPC' is checked.");
                return false;
            }

            return true;
        }
        catch (DllNotFoundException ex)
        {
            Debug.WriteLine($"[EverythingPlugin] Everything64.dll not found next to plugin: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EverythingPlugin] Availability check failed: {ex.Message}");
            return false;
        }
    }

    private static string BuildSearchString(string query, IReadOnlyDictionary<string, string> settings)
    {
        if (settings.TryGetValue(DirectoriesSettingKey, out var directoriesJson) &&
            !string.IsNullOrWhiteSpace(directoriesJson))
        {
            try
            {
                var directories = JsonSerializer.Deserialize<List<string>>(directoriesJson);
                if (directories is { Count: > 0 })
                {
                    var pathFilters = string.Join("|", directories.Select(d => $"path:\"{d}\""));
                    return $"({pathFilters}) {query}";
                }
            }
            catch
            {
            }
        }

        return query;
    }

    private static double ScoreMatch(string rawQuery, string normalizedQuery, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return 0;
        }

        var normalizedName = Normalize(fileName);
        if (string.IsNullOrEmpty(normalizedName))
        {
            return 0;
        }

        // Exact match (normalized: handles diacritics and case)
        if (normalizedName.Equals(normalizedQuery, StringComparison.Ordinal))
        {
            return 200;
        }

        // Starts with query
        if (normalizedName.StartsWith(normalizedQuery, StringComparison.Ordinal))
        {
            return Math.Max(160, 180 - (normalizedName.Length - normalizedQuery.Length) * 0.2);
        }

        // Contains query
        var containsIndex = normalizedName.IndexOf(normalizedQuery, StringComparison.Ordinal);
        if (containsIndex >= 0)
        {
            return Math.Max(100, 140 - containsIndex * 2);
        }

        // Fallback: case-insensitive raw matching for non-normalized characters
        var lowerFileName = fileName.ToLowerInvariant();
        var lowerQuery = rawQuery.ToLowerInvariant();

        if (lowerFileName.Equals(lowerQuery, StringComparison.Ordinal))
        {
            return 190;
        }

        if (lowerFileName.StartsWith(lowerQuery, StringComparison.Ordinal))
        {
            return Math.Max(150, 170 - (lowerFileName.Length - lowerQuery.Length) * 0.2);
        }

        containsIndex = lowerFileName.IndexOf(lowerQuery, StringComparison.Ordinal);
        if (containsIndex >= 0)
        {
            return Math.Max(90, 130 - containsIndex * 2);
        }

        // Subsequence match (e.g., "ch" matches "Chrome")
        if (IsSubsequence(normalizedQuery, normalizedName, out var compactness))
        {
            return Math.Max(60, 80 - compactness);
        }

        // Low baseline score — Everything returned it, so it does match something
        return 40;
    }

    private static string Normalize(string value)
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
}
