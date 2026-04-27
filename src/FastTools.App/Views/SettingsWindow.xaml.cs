using FastTools.App.Models;
using FastTools.App.Services;
using FastTools.Plugin.Abstractions.Contracts;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace FastTools.App.Views;

public partial class SettingsWindow : Window, INotifyPropertyChanged
{
    private readonly LauncherSettings _baseSettings;
    private readonly LocalizationService _localizer;
    private readonly ThemeService _themeService;
    private readonly AppThemeMode _originalTheme;
    private readonly string _originalLanguage;
    private CustomCommandDefinition? _selectedCustomCommand;
    private SearchGroupPriorityItem? _selectedSearchGroupPriority;
    private SettingsSectionItem? _selectedSection;
    private string _hotKeyText = "Alt+Space";
    private AppThemeMode _selectedTheme;
    private string _selectedLanguage = LocalizationService.DefaultLanguageCode;

    public SettingsWindow(
        LauncherSettings settings,
        IReadOnlyList<PluginHostService.LoadedPlugin> plugins,
        LocalizationService localizer,
        ThemeService themeService)
    {
        _baseSettings = settings.Clone();
        _baseSettings.EnsureDefaults();
        settings = _baseSettings.Clone();
        _localizer = localizer;
        _themeService = themeService;
        _originalTheme = themeService.CurrentTheme;
        _originalLanguage = localizer.CurrentLanguage;

        ThemeOptions =
        [
            new OptionItem<AppThemeMode> { Value = AppThemeMode.Light, Label = _localizer.Get("Theme.Light") },
            new OptionItem<AppThemeMode> { Value = AppThemeMode.Dark, Label = _localizer.Get("Theme.Dark") },
            new OptionItem<AppThemeMode> { Value = AppThemeMode.Soft, Label = _localizer.Get("Theme.Soft") },
            new OptionItem<AppThemeMode> { Value = AppThemeMode.Midnight, Label = _localizer.Get("Theme.Midnight") },
            new OptionItem<AppThemeMode> { Value = AppThemeMode.Sepia, Label = _localizer.Get("Theme.Sepia") },
        ];

        LanguageOptions = _localizer.AvailableLanguages
            .Select(language => new OptionItem<string>
            {
                Value = language.Code,
                Label = language.DisplayName,
            })
            .ToList();

        Sections =
        [
            new SettingsSectionItem(SettingsSection.General, "\uE713", _localizer.Get("Settings.General.Title"), _localizer.Get("Settings.General.Description")),
            new SettingsSectionItem(SettingsSection.HotKeys, "\uE765", _localizer.Get("Settings.HotKeys.Title"), _localizer.Get("Settings.HotKeys.Description")),
            new SettingsSectionItem(SettingsSection.Commands, "\uE756", _localizer.Get("Settings.CustomCommands.Title"), _localizer.Get("Settings.CustomCommands.Description")),
            new SettingsSectionItem(SettingsSection.Priority, "\uE8D1", _localizer.Get("Settings.Priority.Title"), _localizer.Get("Settings.Priority.Description")),
            new SettingsSectionItem(SettingsSection.Plugins, "\uE943", _localizer.Get("Settings.Plugins.Title"), _localizer.Get("Settings.Plugins.Description")),
        ];

        CustomCommands = new ObservableCollection<CustomCommandDefinition>(settings.CustomCommands);
        SearchGroupPriorities = new ObservableCollection<SearchGroupPriorityItem>(
            settings.SearchGroupPriorities
                .OrderBy(priority => priority.Priority)
                .Select(priority => new SearchGroupPriorityItem
                {
                    Group = priority.Group,
                    DisplayGroup = _localizer.Get($"Group.{priority.Group}"),
                    Priority = priority.Priority,
                    IsEnabled = priority.IsEnabled,
                }));
        RenumberSearchGroupPriorities();

        var pluginStateMap = settings.PluginStates.ToDictionary(state => state.PluginId, StringComparer.OrdinalIgnoreCase);
        PluginStates = new ObservableCollection<PluginConfigurationItem>(
            plugins.Select(loaded =>
            {
                var plugin = loaded.Instance;
                pluginStateMap.TryGetValue(plugin.Id, out var existingState);
                return PluginConfigurationItem.From(plugin, existingState);
            }));

        HotKeyText = settings.HotKey;
        SelectedTheme = settings.ThemeMode;
        SelectedLanguage = settings.Language;
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

    public IReadOnlyList<OptionItem<string>> LanguageOptions { get; }

    public ObservableCollection<CustomCommandDefinition> CustomCommands { get; }

    public ObservableCollection<SearchGroupPriorityItem> SearchGroupPriorities { get; }

    public ObservableCollection<PluginConfigurationItem> PluginStates { get; }

    public LauncherSettings? SavedSettings { get; private set; }

    public string HotKeyText
    {
        get => _hotKeyText;
        set
        {
            if (_hotKeyText == value)
            {
                return;
            }

            _hotKeyText = value;
            OnPropertyChanged();
        }
    }

    public AppThemeMode SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (_selectedTheme == value)
            {
                return;
            }

            _selectedTheme = value;
            OnPropertyChanged();
            _themeService.Apply(value);
        }
    }

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (string.Equals(_selectedLanguage, value, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _selectedLanguage = value ?? LocalizationService.DefaultLanguageCode;
            OnPropertyChanged();
            _localizer.Apply(_selectedLanguage);
            RefreshLocalization();
        }
    }

    public string SelectedSectionTitle => SelectedSection?.Title ?? string.Empty;

    public string SelectedSectionDescription => SelectedSection?.Description ?? string.Empty;

    public Visibility SelectedSectionHeaderVisibility =>
        SelectedSection?.Section is SettingsSection.HotKeys or SettingsSection.Plugins
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility GeneralSectionVisibility => GetSectionVisibility(SettingsSection.General);

    public Visibility HotKeysSectionVisibility => GetSectionVisibility(SettingsSection.HotKeys);

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
            OnPropertyChanged(nameof(SelectedSectionHeaderVisibility));
            OnPropertyChanged(nameof(GeneralSectionVisibility));
            OnPropertyChanged(nameof(HotKeysSectionVisibility));
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

    public SearchGroupPriorityItem? SelectedSearchGroupPriority
    {
        get => _selectedSearchGroupPriority;
        set
        {
            if (_selectedSearchGroupPriority == value)
            {
                return;
            }

            _selectedSearchGroupPriority = value;
            OnPropertyChanged();
        }
    }

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        CenterOnWorkArea();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (SavedSettings is null)
        {
            if (_themeService.CurrentTheme != _originalTheme)
            {
                _themeService.Apply(_originalTheme);
            }

            if (!string.Equals(_localizer.CurrentLanguage, _originalLanguage, StringComparison.OrdinalIgnoreCase))
            {
                _localizer.Apply(_originalLanguage);
            }
        }

        base.OnClosed(e);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Minimized ? WindowState.Normal : WindowState.Minimized;
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
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

    private void HotKeyButton_Click(object sender, RoutedEventArgs e)
    {
        HotKeyCaptureButton.Focus();
    }

    private void HotKeyCaptureButton_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var key = GetActualKey(e);
        if (IsModifierKey(key))
        {
            e.Handled = true;
            return;
        }

        var modifiers = Keyboard.Modifiers;
        HotKeyText = new HotKeyGesture
        {
            Modifiers = modifiers,
            Key = key,
        }.ToString();
        e.Handled = true;
    }

    private static Key GetActualKey(System.Windows.Input.KeyEventArgs e)
    {
        return e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            Key.DeadCharProcessed => e.DeadCharProcessedKey,
            _ => e.Key,
        };
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl or
            Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or
            Key.LWin or Key.RWin;
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

    private System.Windows.Point _priorityDragStart;
    private SearchGroupPriorityItem? _priorityDragItem;
    private DragAdorner? _dragAdorner;
    private AdornerLayer? _dragAdornerLayer;

    private void PriorityList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _priorityDragStart = e.GetPosition(null);
        _priorityDragItem = FindAncestorPriorityItem(e.OriginalSource as DependencyObject);
    }

    private void PriorityList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _priorityDragItem is null)
        {
            return;
        }

        var current = e.GetPosition(null);
        if (Math.Abs(current.X - _priorityDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _priorityDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var draggedItem = _priorityDragItem;
        _priorityDragItem = null;
        var container = PriorityList.ItemContainerGenerator.ContainerFromItem(draggedItem) as UIElement;
        if (container is null)
        {
            return;
        }

        _dragAdornerLayer = AdornerLayer.GetAdornerLayer(PriorityList);
        if (_dragAdornerLayer is not null)
        {
            var pos = e.GetPosition(PriorityList);
            _dragAdorner = new DragAdorner(PriorityList, container, pos);
            _dragAdornerLayer.Add(_dragAdorner);
        }

        var data = new System.Windows.DataObject(typeof(SearchGroupPriorityItem), draggedItem);
        try
        {
            System.Windows.DragDrop.DoDragDrop(PriorityList, data, System.Windows.DragDropEffects.Move);
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            RemoveDragAdorner();
        }
    }

    private void PriorityList_GiveFeedback(object sender, System.Windows.GiveFeedbackEventArgs e)
    {
        if (e.Effects.HasFlag(System.Windows.DragDropEffects.Move))
        {
            e.UseDefaultCursors = false;
            Mouse.SetCursor(System.Windows.Input.Cursors.SizeAll);
        }
        else
        {
            e.UseDefaultCursors = true;
        }

        e.Handled = true;
    }

    private void PriorityItem_DragEnter(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBoxItem listBoxItem &&
            e.Data.GetDataPresent(typeof(SearchGroupPriorityItem)))
        {
            listBoxItem.Tag = "DropTarget";
        }
    }

    private void PriorityItem_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBoxItem listBoxItem)
        {
            listBoxItem.Tag = null;
        }
    }

    private void PriorityList_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(SearchGroupPriorityItem))
            ? System.Windows.DragDropEffects.Move
            : System.Windows.DragDropEffects.None;

        if (_dragAdorner is not null)
        {
            _dragAdorner.UpdatePosition(e.GetPosition(PriorityList));
        }

        e.Handled = true;
    }

    private void RemoveDragAdorner()
    {
        if (_dragAdorner is not null && _dragAdornerLayer is not null)
        {
            _dragAdornerLayer.Remove(_dragAdorner);
        }

        _dragAdorner = null;
        _dragAdornerLayer = null;
    }

    private void PriorityList_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(typeof(SearchGroupPriorityItem)) is not SearchGroupPriorityItem dragged)
        {
            return;
        }

        var targetItem = FindAncestorPriorityItem(e.OriginalSource as DependencyObject);
        var oldIndex = SearchGroupPriorities.IndexOf(dragged);
        if (oldIndex < 0)
        {
            return;
        }

        var newIndex = targetItem is null ? SearchGroupPriorities.Count - 1 : SearchGroupPriorities.IndexOf(targetItem);
        if (newIndex < 0 || newIndex == oldIndex)
        {
            return;
        }

        SearchGroupPriorities.Move(oldIndex, newIndex);
        RenumberSearchGroupPriorities();
        SelectedSearchGroupPriority = dragged;

        if (sender is System.Windows.Controls.ListBoxItem dropTarget)
        {
            dropTarget.Tag = null;
        }

        e.Handled = true;
    }

    private static SearchGroupPriorityItem? FindAncestorPriorityItem(DependencyObject? source)
    {
        while (source is not null && source is not System.Windows.Controls.ListBoxItem)
        {
            source = source is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(source)
                : System.Windows.LogicalTreeHelper.GetParent(source);
        }

        return (source as System.Windows.Controls.ListBoxItem)?.DataContext as SearchGroupPriorityItem;
    }

    private void RenumberSearchGroupPriorities()
    {
        for (var i = 0; i < SearchGroupPriorities.Count; i++)
        {
            SearchGroupPriorities[i].Priority = (i + 1) * 10;
        }
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
                IsEnabled = priority.IsEnabled,
            })
            .ToList();
        SavedSettings.PluginStates = PluginStates
            .Select(plugin => plugin.ToState())
            .ToList();

        DialogResult = true;
    }

    private void AddPluginDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: PluginDirectoryListSettingItem setting })
        {
            return;
        }

        using var dialog = new System.Windows.Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            setting.AddDirectory(dialog.SelectedPath);
        }
    }

    private void RemovePluginDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { DataContext: string path, Tag: PluginDirectoryListSettingItem setting })
        {
            return;
        }

        setting.RemoveDirectory(path);
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

    private void RefreshLocalization()
    {
        var currentSection = SelectedSection?.Section;
        Sections.Clear();
        Sections.Add(new SettingsSectionItem(SettingsSection.General, "\uE713", _localizer.Get("Settings.General.Title"), _localizer.Get("Settings.General.Description")));
        Sections.Add(new SettingsSectionItem(SettingsSection.HotKeys, "\uE765", _localizer.Get("Settings.HotKeys.Title"), _localizer.Get("Settings.HotKeys.Description")));
        Sections.Add(new SettingsSectionItem(SettingsSection.Commands, "\uE756", _localizer.Get("Settings.CustomCommands.Title"), _localizer.Get("Settings.CustomCommands.Description")));
        Sections.Add(new SettingsSectionItem(SettingsSection.Priority, "\uE8D1", _localizer.Get("Settings.Priority.Title"), _localizer.Get("Settings.Priority.Description")));
        Sections.Add(new SettingsSectionItem(SettingsSection.Plugins, "\uE943", _localizer.Get("Settings.Plugins.Title"), _localizer.Get("Settings.Plugins.Description")));
        SelectedSection = Sections.FirstOrDefault(s => s.Section == currentSection) ?? Sections[0];

        foreach (var option in ThemeOptions)
        {
            option.Label = option.Value switch
            {
                AppThemeMode.Light => _localizer.Get("Theme.Light"),
                AppThemeMode.Dark => _localizer.Get("Theme.Dark"),
                AppThemeMode.Soft => _localizer.Get("Theme.Soft"),
                AppThemeMode.Midnight => _localizer.Get("Theme.Midnight"),
                AppThemeMode.Sepia => _localizer.Get("Theme.Sepia"),
                _ => option.Label,
            };
        }

        foreach (var item in SearchGroupPriorities)
        {
            item.DisplayGroup = _localizer.Get($"Group.{item.Group}");
        }

        ApplyLocalizedColumnHeaders();
    }

    private void ApplyLocalizedColumnHeaders()
    {
        CommandNameColumn.Header = _localizer.Get("Settings.Name");
        CommandAliasColumn.Header = _localizer.Get("Settings.Alias");
        CommandPathColumn.Header = _localizer.Get("Settings.Command");
        CommandArgsColumn.Header = _localizer.Get("Settings.Arguments");
        CommandConfirmColumn.Header = _localizer.Get("Settings.Confirm");
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed class SearchGroupPriorityItem : INotifyPropertyChanged
    {
        private int _priority;
        private bool _isEnabled = true;

        public string Group { get; init; } = string.Empty;

        public string DisplayGroup
        {
            get => _displayGroup;
            set
            {
                if (_displayGroup == value)
                {
                    return;
                }

                _displayGroup = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayGroup)));
            }
        }

        private string _displayGroup = string.Empty;

        public int Priority
        {
            get => _priority;
            set
            {
                if (_priority == value)
                {
                    return;
                }

                _priority = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Priority)));
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value)
                {
                    return;
                }

                _isEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public sealed record SettingsSectionItem(
        SettingsSection Section,
        string Glyph,
        string Title,
        string Description);

    public enum SettingsSection
    {
        General,
        HotKeys,
        Commands,
        Priority,
        Plugins,
    }

    public sealed class PluginConfigurationItem : INotifyPropertyChanged
    {
        private bool _isEnabled = true;
        private bool _isExpanded;

        public string PluginId { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public ObservableCollection<PluginSettingItem> Settings { get; init; } = [];

        public bool HasSettings => Settings.Count > 0;

        public Visibility HasSettingsVisibility => HasSettings ? Visibility.Visible : Visibility.Collapsed;

        public Visibility EmptySettingsVisibility => HasSettings ? Visibility.Collapsed : Visibility.Visible;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value)
                {
                    return;
                }

                _isEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                {
                    return;
                }

                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public static PluginConfigurationItem From(ILauncherPlugin plugin, PluginState? existing)
        {
            var values = existing?.Settings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var settings = plugin.GetConfiguration().Settings
                .Select(setting => PluginSettingItem.Create(setting, values))
                .ToList();

            return new PluginConfigurationItem
            {
                PluginId = plugin.Id,
                DisplayName = plugin.DisplayName,
                Description = plugin.Description,
                IsEnabled = existing?.IsEnabled ?? true,
                Settings = new ObservableCollection<PluginSettingItem>(settings),
                IsExpanded = false,
            };
        }

        public PluginState ToState()
        {
            var settings = Settings.ToDictionary(
                setting => setting.Key,
                setting => setting.SerializeValue(),
                StringComparer.OrdinalIgnoreCase);

            return new PluginState
            {
                PluginId = PluginId,
                DisplayName = DisplayName,
                Description = Description,
                IsEnabled = IsEnabled,
                Settings = settings,
            };
        }
    }

    public abstract class PluginSettingItem
    {
        protected PluginSettingItem(string key, string label, string? description)
        {
            Key = key;
            Label = label;
            Description = description ?? string.Empty;
        }

        public string Key { get; }

        public string Label { get; }

        public string Description { get; }

        public Visibility DescriptionVisibility =>
            string.IsNullOrWhiteSpace(Description) ? Visibility.Collapsed : Visibility.Visible;

        public abstract string SerializeValue();

        public static PluginSettingItem Create(
            PluginSettingDefinition definition,
            IReadOnlyDictionary<string, string> values)
        {
            values.TryGetValue(definition.Key, out var storedValue);
            return definition switch
            {
                PluginSelectSettingDefinition select => PluginSelectSettingItem.From(select, storedValue),
                PluginDirectoryListSettingDefinition directories => PluginDirectoryListSettingItem.From(directories, storedValue),
                _ => new PluginUnknownSettingItem(definition.Key, definition.Label, definition.Description),
            };
        }
    }

    public sealed class PluginSelectSettingItem : PluginSettingItem, INotifyPropertyChanged
    {
        private string _selectedOption = string.Empty;

        private PluginSelectSettingItem(
            string key,
            string label,
            string? description,
            IReadOnlyList<string> options,
            string selectedOption)
            : base(key, label, description)
        {
            Options = new ObservableCollection<string>(options);
            _selectedOption = selectedOption;
        }

        public ObservableCollection<string> Options { get; }

        public string SelectedOption
        {
            get => _selectedOption;
            set
            {
                if (_selectedOption == value)
                {
                    return;
                }

                _selectedOption = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedOption)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public override string SerializeValue()
        {
            return SelectedOption;
        }

        public static PluginSelectSettingItem From(PluginSelectSettingDefinition definition, string? storedValue)
        {
            var selected = definition.Options.Contains(storedValue, StringComparer.OrdinalIgnoreCase)
                ? storedValue!
                : definition.DefaultValue;

            return new PluginSelectSettingItem(
                definition.Key,
                definition.Label,
                definition.Description,
                definition.Options,
                selected);
        }
    }

    public sealed class PluginDirectoryListSettingItem : PluginSettingItem
    {
        private PluginDirectoryListSettingItem(
            string key,
            string label,
            string? description,
            IReadOnlyList<string> directories)
            : base(key, label, description)
        {
            Directories = new ObservableCollection<string>(directories);
        }

        public ObservableCollection<string> Directories { get; }

        public override string SerializeValue()
        {
            return JsonSerializer.Serialize(Directories);
        }

        public void AddDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (Directories.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            Directories.Add(path);
        }

        public void RemoveDirectory(string path)
        {
            var existing = Directories.FirstOrDefault(item => item.Equals(path, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                return;
            }

            Directories.Remove(existing);
        }

        public static PluginDirectoryListSettingItem From(PluginDirectoryListSettingDefinition definition, string? storedValue)
        {
            var directories = new List<string>();
            if (!string.IsNullOrWhiteSpace(storedValue))
            {
                try
                {
                    directories = JsonSerializer.Deserialize<List<string>>(storedValue) ?? [];
                }
                catch
                {
                }
            }

            directories = directories
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new PluginDirectoryListSettingItem(
                definition.Key,
                definition.Label,
                definition.Description,
                directories);
        }
    }

    public sealed class PluginUnknownSettingItem : PluginSettingItem
    {
        public PluginUnknownSettingItem(string key, string label, string? description)
            : base(key, label, description)
        {
        }

        public override string SerializeValue()
        {
            return string.Empty;
        }
    }
}
