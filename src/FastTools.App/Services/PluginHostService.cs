using FastTools.App.Models;
using FastTools.Plugin.Abstractions.Contracts;
using System.Runtime.Loader;
using System.Text.Json;

namespace FastTools.App.Services;

public sealed class PluginHostService
{
    private const string WebPluginId = "fasttools.web";
    private const string FolderIndexPluginId = "fasttools.folderindex";
    private const string EverythingPluginId = "fasttools.everything";
    private const string WebDefaultEngineSettingKey = "default_engine";
    private const string FolderIndexDirectoriesSettingKey = "index_directories_json";
    private const string EverythingDirectoriesSettingKey = "scope_directories_json";

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
        return GetPluginState(plugin.Instance)?.IsEnabled ?? true;
    }

    private IReadOnlyDictionary<string, string> BuildPluginSettings(LoadedPlugin plugin)
    {
        var state = GetPluginState(plugin.Instance);
        if (state?.Settings is null)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return state.Settings
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<SearchResultItem>> SearchPluginAsync(
        LoadedPlugin plugin,
        string query,
        CancellationToken cancellationToken)
    {
        try
        {
            var items = await plugin.Instance.QueryAsync(
                new PluginQuery(query, BuildPluginSettings(plugin)),
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
                    new PluginExecutionRequest(item, BuildPluginSettings(plugin)),
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
        var state = GetPluginState(plugin);
        if (state is null)
        {
            state = new PluginState
            {
                PluginId = plugin.Id,
                DisplayName = plugin.DisplayName,
                Description = plugin.Description,
                IsEnabled = true,
                Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            };
            _settingsStore.Current.PluginStates.Add(state);
        }

        state.DisplayName = plugin.DisplayName;
        state.Description = plugin.Description;
        state.Settings ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var configuration = plugin.GetConfiguration();
        foreach (var setting in configuration.Settings)
        {
            if (state.Settings.ContainsKey(setting.Key))
            {
                continue;
            }

            state.Settings[setting.Key] = ResolveDefaultValue(plugin.Id, setting);
        }
    }

    private PluginState? GetPluginState(ILauncherPlugin plugin)
    {
        return _settingsStore.Current.PluginStates
            .FirstOrDefault(state => state.PluginId.Equals(plugin.Id, StringComparison.OrdinalIgnoreCase));
    }

    private string ResolveDefaultValue(string pluginId, PluginSettingDefinition setting)
    {
        if (TryReadLegacyValue(pluginId, setting.Key, out var legacyValue))
        {
            return legacyValue;
        }

        return setting switch
        {
            PluginSelectSettingDefinition select => select.DefaultValue,
            PluginDirectoryListSettingDefinition => "[]",
            _ => string.Empty,
        };
    }

    private bool TryReadLegacyValue(string pluginId, string settingKey, out string value)
    {
        if (pluginId.Equals(WebPluginId, StringComparison.OrdinalIgnoreCase) &&
            settingKey.Equals(WebDefaultEngineSettingKey, StringComparison.OrdinalIgnoreCase))
        {
            value = _settingsStore.Current.DefaultSearchEngine;
            return !string.IsNullOrWhiteSpace(value);
        }

        if (pluginId.Equals(FolderIndexPluginId, StringComparison.OrdinalIgnoreCase) &&
            settingKey.Equals(FolderIndexDirectoriesSettingKey, StringComparison.OrdinalIgnoreCase) &&
            _settingsStore.Current.IndexedDirectories is { Count: > 0 })
        {
            value = JsonSerializer.Serialize(_settingsStore.Current.IndexedDirectories);
            return true;
        }

        if (pluginId.Equals(EverythingPluginId, StringComparison.OrdinalIgnoreCase) &&
            settingKey.Equals(EverythingDirectoriesSettingKey, StringComparison.OrdinalIgnoreCase) &&
            _settingsStore.Current.EverythingIndexedDirectories is { Count: > 0 })
        {
            value = JsonSerializer.Serialize(_settingsStore.Current.EverythingIndexedDirectories);
            return true;
        }

        value = string.Empty;
        return false;
    }
}
