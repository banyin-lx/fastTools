namespace FastTools.App.Models;

public sealed class LauncherSettings
{
    public string HotKey { get; set; } = "Alt+Space";

    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.Dark;

    public AppLanguage Language { get; set; } = AppLanguage.ZhCn;

    public string DefaultSearchEngine { get; set; } = "Bing";

    public List<string> IndexedDirectories { get; set; } = [];

    public List<string> ApplicationDirectories { get; set; } = [];

    public List<CustomCommandDefinition> CustomCommands { get; set; } = [];

    public List<PluginState> PluginStates { get; set; } = [];

    public List<SearchGroupPriority> SearchGroupPriorities { get; set; } = [];

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
            IndexedDirectories = [.. IndexedDirectories],
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
                })
                .ToList(),
            SearchGroupPriorities = SearchGroupPriorities
                .Select(priority => new SearchGroupPriority
                {
                    Group = priority.Group,
                    Priority = priority.Priority,
                })
                .ToList(),
            UsageCounts = UsageCounts.ToDictionary(pair => pair.Key, pair => pair.Value),
        };
    }

    public int GetPriorityForGroup(string group)
    {
        var configured = SearchGroupPriorities
            .FirstOrDefault(item => item.Group.Equals(group, StringComparison.OrdinalIgnoreCase));
        return configured?.Priority ?? 999;
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
