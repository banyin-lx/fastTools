using FastTools.App.Infrastructure;
using System.Text.Json.Serialization;

namespace FastTools.App.Models;

public sealed class LauncherSettings
{
    public const int DefaultSearchDebounceMilliseconds = 120;

    public string HotKey { get; set; } = "Alt+Space";

    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.Dark;

    [JsonConverter(typeof(LegacyLanguageConverter))]
    public string Language { get; set; } = "zh-CN";

    public string DefaultSearchEngine { get; set; } = "Bing";

    public bool HideShortcutResults { get; set; }

    public bool SearchDebounceEnabled { get; set; } = true;

    public int SearchDebounceMilliseconds { get; set; } = DefaultSearchDebounceMilliseconds;

    public bool LoggingEnabled { get; set; } = true;

    public LogLevel MinLogLevel { get; set; } = LogLevel.Info;

    public List<string> IndexedDirectories { get; set; } = [];

    public List<string> ApplicationDirectories { get; set; } = [];

    public List<string> EverythingIndexedDirectories { get; set; } = [];

    public List<CustomCommandDefinition> CustomCommands { get; set; } = [];

    public List<PluginState> PluginStates { get; set; } = [];

    public List<SearchGroupPriority> SearchGroupPriorities { get; set; } = [];

    public SearchBarHorizontalPosition HorizontalPosition { get; set; } = SearchBarHorizontalPosition.Center;

    public SearchBarVerticalPosition VerticalPosition { get; set; } = SearchBarVerticalPosition.Top;

    public SearchWindowPositionMode WindowPositionMode { get; set; } = SearchWindowPositionMode.RememberLast;

    public Dictionary<string, int> UsageCounts { get; set; } = [];

    public static LauncherSettings CreateDefault()
    {
        return new LauncherSettings
        {
            IndexedDirectories = BuildDefaultIndexedDirectories(),
            ApplicationDirectories = [],
            SearchGroupPriorities = BuildDefaultSearchGroupPriorities(),
            CustomCommands =
            [
                new CustomCommandDefinition
                {
                    Name = "PowerShell",
                    Alias = "ps",
                    Command = "powershell.exe",
                    Arguments = string.Empty,
                },
                new CustomCommandDefinition
                {
                    Name = "CMD",
                    Alias = "cmd",
                    Command = "cmd.exe",
                    Arguments = string.Empty,
                },
            ],
        };
    }

    public LauncherSettings Clone()
    {
        return new LauncherSettings
        {
            HotKey = HotKey,
            ThemeMode = ThemeMode,
            Language = Language,
            DefaultSearchEngine = DefaultSearchEngine,
            HideShortcutResults = HideShortcutResults,
            SearchDebounceEnabled = SearchDebounceEnabled,
            SearchDebounceMilliseconds = SearchDebounceMilliseconds,
            LoggingEnabled = LoggingEnabled,
            MinLogLevel = MinLogLevel,
            IndexedDirectories = [.. IndexedDirectories],
            EverythingIndexedDirectories = [.. EverythingIndexedDirectories],
            ApplicationDirectories = [.. ApplicationDirectories],
            CustomCommands = CustomCommands
                .Select(command => new CustomCommandDefinition
                {
                    Id = command.Id,
                    Name = command.Name,
                    Alias = command.Alias,
                    Command = command.Command,
                    Arguments = command.Arguments,
                    RequiresConfirmation = command.RequiresConfirmation,
                    ConfirmationMessage = command.ConfirmationMessage,
                })
                .ToList(),
            PluginStates = PluginStates
                .Select(plugin => new PluginState
                {
                    PluginId = plugin.PluginId,
                    DisplayName = plugin.DisplayName,
                    Description = plugin.Description,
                    IsEnabled = plugin.IsEnabled,
                    Settings = plugin.Settings?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? [],
                })
                .ToList(),
            SearchGroupPriorities = SearchGroupPriorities
                .Select(priority => new SearchGroupPriority
                {
                    Group = priority.Group,
                    Priority = priority.Priority,
                    IsEnabled = priority.IsEnabled,
                })
                .ToList(),
            HorizontalPosition = HorizontalPosition,
            VerticalPosition = VerticalPosition,
            WindowPositionMode = WindowPositionMode,
            UsageCounts = UsageCounts.ToDictionary(pair => pair.Key, pair => pair.Value),
        };
    }

    public void EnsureDefaults()
    {
        if (SearchDebounceMilliseconds < 0)
        {
            SearchDebounceMilliseconds = DefaultSearchDebounceMilliseconds;
        }

        if (SearchGroupPriorities is null || SearchGroupPriorities.Count == 0)
        {
            SearchGroupPriorities = BuildDefaultSearchGroupPriorities();
        }

        // Merge any newly-known groups while preserving the user's existing order.
        var existing = new HashSet<string>(
            SearchGroupPriorities.Select(item => item.Group),
            StringComparer.OrdinalIgnoreCase);

        foreach (var fallback in BuildDefaultSearchGroupPriorities())
        {
            if (existing.Add(fallback.Group))
            {
                SearchGroupPriorities.Add(new SearchGroupPriority
                {
                    Group = fallback.Group,
                    Priority = (SearchGroupPriorities.Count + 1) * 10,
                });
            }
        }
    }

    public int GetPriorityForGroup(string group)
    {
        var configured = SearchGroupPriorities
            .FirstOrDefault(item => item.Group.Equals(group, StringComparison.OrdinalIgnoreCase));
        return configured?.Priority ?? 999;
    }

    public bool IsGroupEnabled(string group)
    {
        var configured = SearchGroupPriorities
            .FirstOrDefault(item => item.Group.Equals(group, StringComparison.OrdinalIgnoreCase));
        return configured?.IsEnabled ?? true;
    }

    private static List<string> BuildDefaultIndexedDirectories()
    {
        var candidates = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
        };

        return candidates
            .Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<SearchGroupPriority> BuildDefaultSearchGroupPriorities()
    {
        return
        [
            new SearchGroupPriority { Group = "Applications", Priority = 10 },
            new SearchGroupPriority { Group = "Commands", Priority = 20 },
            new SearchGroupPriority { Group = "Files", Priority = 30 },
            new SearchGroupPriority { Group = "Web", Priority = 40 },
        ];
    }
}
