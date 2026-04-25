using FastTools.App.Models;
using FastTools.Plugin.Abstractions.Contracts;
using System.Runtime.Loader;

namespace FastTools.App.Services;

public sealed class PluginHostService
{
    private readonly LauncherSettingsStore _settingsStore;
    private readonly string _pluginDirectory;
    private readonly List<LoadedPlugin> _plugins = [];

    public PluginHostService(LauncherSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        _pluginDirectory = Path.Combine(AppContext.BaseDirectory, "Plugins");
    }

    public IReadOnlyList<LoadedPlugin> Plugins => _plugins;

    public Task LoadAsync()
    {
        _plugins.Clear();

        if (!Directory.Exists(_pluginDirectory))
        {
            return Task.CompletedTask;
        }

        var loadedPluginIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var assemblyPath in Directory.EnumerateFiles(_pluginDirectory, "*.dll", SearchOption.AllDirectories))
        {
            try
            {
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
                var types = assembly.GetTypes()
                    .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                                   typeof(ILauncherPlugin).IsAssignableFrom(type));

                foreach (var type in types)
                {
                    if (Activator.CreateInstance(type) is not ILauncherPlugin plugin)
                    {
                        continue;
                    }

                    if (!loadedPluginIds.Add(plugin.Id))
                    {
                        continue;
                    }

                    _plugins.Add(new LoadedPlugin(plugin, assemblyPath));
                    UpsertPluginState(plugin);
                }
            }
            catch
            {
            }
        }

        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<SearchResultItem>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (_plugins.Count == 0)
        {
            return [];
        }

        var tasks = _plugins
            .Where(IsEnabled)
            .Select(plugin => SearchPluginAsync(plugin, query, cancellationToken));
        var batches = await Task.WhenAll(tasks).ConfigureAwait(false);
        return batches.SelectMany(batch => batch).ToList();
    }

    public sealed record LoadedPlugin(ILauncherPlugin Instance, string AssemblyPath);

    private bool IsEnabled(LoadedPlugin plugin)
    {
        return _settingsStore.Current.PluginStates
            .FirstOrDefault(state => state.PluginId.Equals(plugin.Instance.Id, StringComparison.OrdinalIgnoreCase))
            ?.IsEnabled ?? true;
    }

    private IReadOnlyDictionary<string, string> BuildPluginSettings()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DefaultSearchEngine"] = _settingsStore.Current.DefaultSearchEngine,
        };
    }

    private async Task<IReadOnlyList<SearchResultItem>> SearchPluginAsync(
        LoadedPlugin plugin,
        string query,
        CancellationToken cancellationToken)
    {
        try
        {
            var items = await plugin.Instance.QueryAsync(
                new PluginQuery(query, BuildPluginSettings()),
                cancellationToken).ConfigureAwait(false);

            return items.Select(item => new SearchResultItem
            {
                Key = $"plugin:{plugin.Instance.Id}:{item.Key}",
                Title = item.Title,
                Subtitle = item.Subtitle,
                Group = item.Category,
                Glyph = item.Glyph,
                Score = item.Score,
                RequiresConfirmation = item.RequiresConfirmation,
                ConfirmationMessage = item.ConfirmationMessage,
                ExecuteAsync = ct => plugin.Instance.ExecuteAsync(
                    new PluginExecutionRequest(item, BuildPluginSettings()),
                    ct),
            }).ToList();
        }
        catch
        {
            return [];
        }
    }

    private void UpsertPluginState(ILauncherPlugin plugin)
    {
        var state = _settingsStore.Current.PluginStates
            .FirstOrDefault(existing => existing.PluginId.Equals(plugin.Id, StringComparison.OrdinalIgnoreCase));

        if (state is not null)
        {
            state.DisplayName = plugin.DisplayName;
            state.Description = plugin.Description;
            return;
        }

        _settingsStore.Current.PluginStates.Add(new PluginState
        {
            PluginId = plugin.Id,
            DisplayName = plugin.DisplayName,
            Description = plugin.Description,
            IsEnabled = true,
        });
    }
}
