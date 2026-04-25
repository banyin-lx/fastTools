using FastTools.App.Models;
using FastTools.App.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace FastTools.App.Views;

public partial class SettingsWindow : Window, INotifyPropertyChanged
{
    private readonly LauncherSettings _baseSettings;
    private readonly LocalizationService _localizer;
    private CustomCommandDefinition? _selectedCustomCommand;
    private SettingsSectionItem? _selectedSection;

    public SettingsWindow(
        LauncherSettings settings,
        IReadOnlyList<PluginHostService.LoadedPlugin> plugins,
        LocalizationService localizer)
    {
        _baseSettings = settings.Clone();
        _localizer = localizer;

        ThemeOptions =
        [
            new OptionItem<AppThemeMode> { Value = AppThemeMode.Dark, Label = _localizer.Get("Theme.Dark") },
            new OptionItem<AppThemeMode> { Value = AppThemeMode.Light, Label = _localizer.Get("Theme.Light") },
        ];

        LanguageOptions =
        [
            new OptionItem<AppLanguage> { Value = AppLanguage.ZhCn, Label = _localizer.Get("Language.ZhCn") },
            new OptionItem<AppLanguage> { Value = AppLanguage.EnUs, Label = _localizer.Get("Language.EnUs") },
        ];

        SearchEngines = ["Bing", "Google", "Baidu", "GitHub", "Bilibili"];

        Sections =
        [
            new SettingsSectionItem(SettingsSection.General, "\uE713", _localizer.Get("Settings.General.Title"), _localizer.Get("Settings.General.Description")),
            new SettingsSectionItem(SettingsSection.Indexing, "\uE9CE", _localizer.Get("Settings.Indexing.Title"), _localizer.Get("Settings.Indexing.Description")),
            new SettingsSectionItem(SettingsSection.Commands, "\uE756", _localizer.Get("Settings.CustomCommands.Title"), _localizer.Get("Settings.CustomCommands.Description")),
            new SettingsSectionItem(SettingsSection.Priority, "\uE8D1", _localizer.Get("Settings.Priority.Title"), _localizer.Get("Settings.Priority.Description")),
            new SettingsSectionItem(SettingsSection.Plugins, "\uE943", _localizer.Get("Settings.Plugins.Title"), _localizer.Get("Settings.Plugins.Description")),
        ];

        CustomCommands = new ObservableCollection<CustomCommandDefinition>(settings.CustomCommands);
        SearchGroupPriorities = new ObservableCollection<SearchGroupPriorityItem>(
            settings.SearchGroupPriorities.Select(priority => new SearchGroupPriorityItem
            {
                Group = priority.Group,
                DisplayGroup = _localizer.Get($"Group.{priority.Group}"),
                Priority = priority.Priority,
            }));

        var pluginStateMap = settings.PluginStates.ToDictionary(state => state.PluginId, StringComparer.OrdinalIgnoreCase);
        PluginStates = new ObservableCollection<PluginState>(
            plugins.Select(plugin =>
            {
                if (pluginStateMap.TryGetValue(plugin.Instance.Id, out var existing))
                {
                    return new PluginState
                    {
                        PluginId = existing.PluginId,
                        DisplayName = plugin.Instance.DisplayName,
                        Description = plugin.Instance.Description,
                        IsEnabled = existing.IsEnabled,
                    };
                }

                return new PluginState
                {
                    PluginId = plugin.Instance.Id,
                    DisplayName = plugin.Instance.DisplayName,
                    Description = plugin.Instance.Description,
                    IsEnabled = true,
                };
            }));

        HotKeyText = settings.HotKey;
        SelectedTheme = settings.ThemeMode;
        SelectedLanguage = settings.Language;
        SelectedSearchEngine = settings.DefaultSearchEngine;
        ApplicationDirectoriesText = string.Join(Environment.NewLine, settings.ApplicationDirectories);
        IndexedDirectoriesText = string.Join(Environment.NewLine, settings.IndexedDirectories);
        SelectedSection = Sections[0];

        InitializeComponent();
        DataContext = this;
        ApplyLocalizedColumnHeaders();

        Loaded += SettingsWindow_Loaded;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public LocalizationService Localizer => _localizer;

    public ObservableCollection<SettingsSectionItem> Sections { get; }

    public IReadOnlyList<OptionItem<AppThemeMode>> ThemeOptions { get; }

    public IReadOnlyList<OptionItem<AppLanguage>> LanguageOptions { get; }

    public IReadOnlyList<string> SearchEngines { get; }

    public ObservableCollection<CustomCommandDefinition> CustomCommands { get; }

    public ObservableCollection<SearchGroupPriorityItem> SearchGroupPriorities { get; }

    public ObservableCollection<PluginState> PluginStates { get; }

    public LauncherSettings? SavedSettings { get; private set; }

    public string HotKeyText { get; set; } = "Alt+Space";

    public string ApplicationDirectoriesText { get; set; } = string.Empty;

    public string IndexedDirectoriesText { get; set; } = string.Empty;

    public AppThemeMode SelectedTheme { get; set; }

    public AppLanguage SelectedLanguage { get; set; }

    public string SelectedSearchEngine { get; set; } = "Bing";

    public string SelectedSectionTitle => SelectedSection?.Title ?? string.Empty;

    public string SelectedSectionDescription => SelectedSection?.Description ?? string.Empty;

    public Visibility GeneralSectionVisibility => GetSectionVisibility(SettingsSection.General);

    public Visibility IndexingSectionVisibility => GetSectionVisibility(SettingsSection.Indexing);

    public Visibility CommandsSectionVisibility => GetSectionVisibility(SettingsSection.Commands);

    public Visibility PrioritySectionVisibility => GetSectionVisibility(SettingsSection.Priority);

    public Visibility PluginsSectionVisibility => GetSectionVisibility(SettingsSection.Plugins);

    public SettingsSectionItem? SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (_selectedSection == value)
            {
                return;
            }

            _selectedSection = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedSectionTitle));
            OnPropertyChanged(nameof(SelectedSectionDescription));
            OnPropertyChanged(nameof(GeneralSectionVisibility));
            OnPropertyChanged(nameof(IndexingSectionVisibility));
            OnPropertyChanged(nameof(CommandsSectionVisibility));
            OnPropertyChanged(nameof(PrioritySectionVisibility));
            OnPropertyChanged(nameof(PluginsSectionVisibility));
        }
    }

    public CustomCommandDefinition? SelectedCustomCommand
    {
        get => _selectedCustomCommand;
        set
        {
            if (_selectedCustomCommand == value)
            {
                return;
            }

            _selectedCustomCommand = value;
            OnPropertyChanged();
        }
    }

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        CenterOnWorkArea();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseWindowButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void AddCommandButton_Click(object sender, RoutedEventArgs e)
    {
        var command = new CustomCommandDefinition
        {
            Name = "New Command",
            Alias = "new",
            Command = "powershell.exe",
            Arguments = string.Empty,
        };

        CustomCommands.Add(command);
        SelectedCustomCommand = command;
        SelectedSection = Sections.FirstOrDefault(item => item.Section == SettingsSection.Commands);
    }

    private void RemoveCommandButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCustomCommand is null)
        {
            return;
        }

        CustomCommands.Remove(SelectedCustomCommand);
        SelectedCustomCommand = null;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!HotKeyGesture.TryParse(HotKeyText, out _))
        {
            System.Windows.MessageBox.Show(
                _localizer.Get("Settings.InvalidHotKey"),
                _localizer.Get("Settings.Title"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        SavedSettings = _baseSettings.Clone();
        SavedSettings.HotKey = HotKeyText;
        SavedSettings.ThemeMode = SelectedTheme;
        SavedSettings.Language = SelectedLanguage;
        SavedSettings.DefaultSearchEngine = SelectedSearchEngine;
        SavedSettings.ApplicationDirectories = ParseMultiLine(ApplicationDirectoriesText);
        SavedSettings.IndexedDirectories = ParseMultiLine(IndexedDirectoriesText);
        SavedSettings.CustomCommands = CustomCommands
            .Where(command => !string.IsNullOrWhiteSpace(command.Name) &&
                              !string.IsNullOrWhiteSpace(command.Command))
            .ToList();
        SavedSettings.SearchGroupPriorities = SearchGroupPriorities
            .OrderBy(priority => priority.Priority)
            .Select(priority => new SearchGroupPriority
            {
                Group = priority.Group,
                Priority = priority.Priority,
            })
            .ToList();
        SavedSettings.PluginStates = PluginStates.ToList();

        DialogResult = true;
    }

    private static List<string> ParseMultiLine(string input)
    {
        return input
            .Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private Visibility GetSectionVisibility(SettingsSection section)
    {
        return SelectedSection?.Section == section ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CenterOnWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;

        Left = workArea.Left + Math.Max(0d, (workArea.Width - width) / 2d);
        Top = workArea.Top + Math.Max(0d, (workArea.Height - height) / 2d);
    }

    private void ApplyLocalizedColumnHeaders()
    {
        CommandNameColumn.Header = _localizer.Get("Settings.Name");
        CommandAliasColumn.Header = _localizer.Get("Settings.Alias");
        CommandPathColumn.Header = _localizer.Get("Settings.Command");
        CommandArgsColumn.Header = _localizer.Get("Settings.Arguments");
        CommandConfirmColumn.Header = _localizer.Get("Settings.Confirm");
        PriorityGroupColumn.Header = _localizer.Get("Settings.Group");
        PriorityValueColumn.Header = _localizer.Get("Settings.Priority");
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed class SearchGroupPriorityItem
    {
        public string Group { get; init; } = string.Empty;

        public string DisplayGroup { get; init; } = string.Empty;

        public int Priority { get; set; }
    }

    public sealed record SettingsSectionItem(
        SettingsSection Section,
        string Glyph,
        string Title,
        string Description);

    public enum SettingsSection
    {
        General,
        Indexing,
        Commands,
        Priority,
        Plugins,
    }
}
