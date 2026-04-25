using FastTools.Plugin.Abstractions.Contracts;
using System.Diagnostics;

namespace FastTools.Plugin.WebSearch.Plugins;

public sealed class WebSearchPlugin : ILauncherPlugin
{
    private static readonly IReadOnlyDictionary<string, SearchEngine> Engines =
        new Dictionary<string, SearchEngine>(StringComparer.OrdinalIgnoreCase)
        {
            ["Bing"] = new("Bing", "https://www.bing.com/search?q={0}"),
            ["Google"] = new("Google", "https://www.google.com/search?q={0}"),
            ["Baidu"] = new("Baidu", "https://www.baidu.com/s?wd={0}"),
            ["GitHub"] = new("GitHub", "https://github.com/search?q={0}"),
            ["Bilibili"] = new("Bilibili", "https://search.bilibili.com/all?keyword={0}"),
        };

    private static readonly IReadOnlyDictionary<string, string> PrefixMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["g "] = "Google",
            ["b "] = "Baidu",
            ["gh "] = "GitHub",
            ["bi "] = "Bilibili",
        };

    public string Id => "fasttools.web";

    public string DisplayName => "Web Search";

    public string Description => "提供网址直达、默认搜索引擎搜索和搜索前缀支持。";

    public Task<IReadOnlyList<PluginSearchItem>> QueryAsync(PluginQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.Text))
        {
            return Task.FromResult<IReadOnlyList<PluginSearchItem>>([]);
        }

        var text = query.Text.Trim();
        var defaultEngine = query.Settings.TryGetValue("DefaultSearchEngine", out var configuredEngine) &&
                            Engines.ContainsKey(configuredEngine)
            ? configuredEngine
            : "Bing";

        if (TryBuildPrefixedSearch(text, out var engineName, out var rawQuery))
        {
            return Task.FromResult<IReadOnlyList<PluginSearchItem>>(
            [
                BuildSearchItem(engineName, rawQuery),
            ]);
        }

        var results = new List<PluginSearchItem>();
        if (LooksLikeUrl(text))
        {
            results.Add(new PluginSearchItem(
                $"open:{text}",
                $"Open {text}",
                "使用默认浏览器直接打开网址",
                "Web",
                "\uE774",
                170));
        }

        results.Add(BuildSearchItem(defaultEngine, text));

        if (!defaultEngine.Equals("GitHub", StringComparison.OrdinalIgnoreCase))
        {
            results.Add(BuildSearchItem("GitHub", text, 110));
        }

        return Task.FromResult<IReadOnlyList<PluginSearchItem>>(results);
    }

    public Task ExecuteAsync(PluginExecutionRequest request, CancellationToken cancellationToken)
    {
        var target = request.Item.Key.StartsWith("open:", StringComparison.OrdinalIgnoreCase)
            ? EnsureUrl(request.Item.Key["open:".Length..])
            : BuildSearchUrl(request.Item.Key);

        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true,
        });

        return Task.CompletedTask;
    }

    private static PluginSearchItem BuildSearchItem(string engineName, string query, double score = 140)
    {
        return new PluginSearchItem(
            $"{engineName}:{query}",
            $"Search with {engineName}",
            query,
            "Web",
            "\uE774",
            score);
    }

    private static string BuildSearchUrl(string key)
    {
        var separatorIndex = key.IndexOf(':');
        var engineName = key[..separatorIndex];
        var query = key[(separatorIndex + 1)..];
        var engine = Engines[engineName];
        return string.Format(engine.Template, Uri.EscapeDataString(query));
    }

    private static bool TryBuildPrefixedSearch(string input, out string engineName, out string query)
    {
        foreach (var prefix in PrefixMap)
        {
            if (input.StartsWith(prefix.Key, StringComparison.OrdinalIgnoreCase))
            {
                engineName = prefix.Value;
                query = input[prefix.Key.Length..].Trim();
                return !string.IsNullOrWhiteSpace(query);
            }
        }

        engineName = string.Empty;
        query = string.Empty;
        return false;
    }

    private static bool LooksLikeUrl(string value)
    {
        if (value.Contains(' '))
        {
            return false;
        }

        return Uri.TryCreate(EnsureUrl(value), UriKind.Absolute, out _);
    }

    private static string EnsureUrl(string value)
    {
        return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"https://{value}";
    }

    private sealed record SearchEngine(string Name, string Template);
}
