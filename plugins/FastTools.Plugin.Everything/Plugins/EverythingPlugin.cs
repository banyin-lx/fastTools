using FastTools.Plugin.Abstractions.Contracts;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace FastTools.Plugin.Everything.Plugins;

public sealed class EverythingPlugin : ILauncherPlugin
{
    private const string DirectoriesSettingKey = "scope_directories_json";
    private static readonly object Gate = new();

    public string Id => "fasttools.everything";

    public string DisplayName => "Everything Search";

    public string Description => "使用 Everything SDK 进行极速文件搜索。";

    public PluginConfiguration GetConfiguration()
    {
        return new PluginConfiguration(
        [
            new PluginDirectoryListSettingDefinition(
                DirectoriesSettingKey,
                "搜索目录范围,留空则搜索全部 Everything 索引,添加目录后仅在这些目录范围内搜索。"),
        ]);
    }

    public Task<IReadOnlyList<PluginSearchItem>> QueryAsync(PluginQuery query, CancellationToken cancellationToken)
    {
        var trimmed = query.Text.Trim();
        if (trimmed.Length < 2)
        {
            return Task.FromResult<IReadOnlyList<PluginSearchItem>>([]);
        }

        return Task.Run(() => SearchCore(trimmed, query.Settings, cancellationToken), cancellationToken);
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

    private static IReadOnlyList<PluginSearchItem> SearchCore(string query, IReadOnlyDictionary<string, string> settings, CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            try
            {
                if (!EverythingInterop.IsAvailable())
                {
                    return [];
                }

                var searchString = BuildSearchString(query, settings);
                EverythingInterop.Everything_SetSearchW(searchString);
                EverythingInterop.Everything_SetMax(12);
                EverythingInterop.Everything_SetOffset(0);
                EverythingInterop.Everything_SetRequestFlags(
                    EverythingInterop.EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME);
                EverythingInterop.Everything_SetSort(EverythingInterop.EVERYTHING_SORT_NAME_ASCENDING);

                if (!EverythingInterop.Everything_QueryW(true))
                {
                    return [];
                }

                cancellationToken.ThrowIfCancellationRequested();

                var count = EverythingInterop.Everything_GetNumResults();
                if (count == 0)
                {
                    return [];
                }

                var results = new List<PluginSearchItem>((int)Math.Min(count, 12));
                var buffer = new StringBuilder(260);

                for (uint i = 0; i < count && i < 12; i++)
                {
                    buffer.Clear();
                    EverythingInterop.Everything_GetResultFullPathNameW(i, buffer, 260);
                    var fullPath = buffer.ToString();
                    if (string.IsNullOrEmpty(fullPath))
                    {
                        continue;
                    }

                    var isFolder = EverythingInterop.Everything_IsResultFolder(i);
                    var name = Path.GetFileName(fullPath);
                    var parent = Path.GetDirectoryName(fullPath) ?? string.Empty;

                    results.Add(new PluginSearchItem(
                        $"everything:{fullPath}",
                        name,
                        parent,
                        "Files",
                        isFolder ? "\uE838" : "\uE8A5",
                        100));
                }

                return results;
            }
            catch (DllNotFoundException)
            {
                return [];
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return [];
            }
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
}
