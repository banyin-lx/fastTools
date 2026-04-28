using FastTools.App.Infrastructure;
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
    private SearchBarHorizontalPosition _selectedHorizontalPosition;
    private SearchBarVerticalPosition _selectedVerticalPosition;
    private SearchWindowPositionMode _selectedWindowPositionMode;
    private bool _isPositionExpanded = true;
    private bool _hideShortcutResults;
    private bool _searchDebounceEnabled = true;
    private string _searchDebounceMillisecondsText = LauncherSettings.DefaultSearchDebounceMilliseconds.ToString();
    private bool _loggingEnabled = true;
    private LogLevel _selectedLogLevel = LogLevel.Info;

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

        SearchBarHorizontalOptions =
        [
            new OptionItem<SearchBarHorizontalPosition> { Value = SearchBarHorizontalPosition.Left, Label = _localizer.Get("SearchBarPosition.Horizontal.Left") },
            new OptionItem<SearchBarHorizontalPosition> { Value = SearchBarHorizontalPosition.Center, Label = _localizer.Get("SearchBarPosition.Horizontal.Center") },
            new OptionItem<SearchBarHorizontalPosition> { Value = SearchBarHorizontalPosition.Right, Label = _localizer.Get("SearchBarPosition.Horizontal.Right") },
        ];

        SearchBarVerticalOptions =
        [
            new OptionItem<SearchBarVerticalPosition> { Value = SearchBarVerticalPosition.Top, Label = _localizer.Get("SearchBarPosition.Vertical.Top") },
            new OptionItem<SearchBarVerticalPosition> { Value = SearchBarVerticalPosition.Middle, Label = _localizer.Get("SearchBarPosition.Vertical.Middle") },
        ];

        SearchWindowPositionOptions =
        [
            new OptionItem<SearchWindowPositionMode> { Value = SearchWindowPositionMode.RememberLast, Label = _localizer.Get("WindowPosition.RememberLast") },
            new OptionItem<SearchWindowPositionMode> { Value = SearchWindowPositionMode.FollowMouse, Label = _localizer.Get("WindowPosition.FollowMouse") },
            new OptionItem<SearchWindowPositionMode> { Value = SearchWindowPositionMode.PrimaryMonitor, Label = _localizer.Get("WindowPosition.PrimaryMonitor") },
        ];

        Sections =
        [
            new SettingsSectionItem(SettingsSection.General, "\uE713", _localizer.Get("Settings.General.Title"), _localizer.Get("Settings.General.Description")),
            new SettingsSectionItem(SettingsSection.HotKeys, "\uE765", _localizer.Get("Settings.HotKeys.Title"), _localizer.Get("Settings.HotKeys.Description")),
            new SettingsSectionItem(SettingsSection.Commands, "\uE756", _localizer.Get("Settings.CustomCommands.Title"), _localizer.Get("Settings.CustomCommands.Description")),
            new SettingsSectionItem(SettingsSection.Priority, "\uE8D1", _localizer.Get("Settings.Priority.Title"), _localizer.Get("Settings.Priority.Description")),
            new SettingsSectionItem(SettingsSection.Plugins, "\uE943", _localizer.Get("Settings.Plugins.Title"), _localizer.Get("Settings.Plugins.Description")),
            new SettingsSectionItem(SettingsSection.Logs, "\uE9F9", _localizer.Get("Settings.Logs.Title"), _localizer.Get("Settings.Logs.Description")),
            new SettingsSectionItem(SettingsSection.About, "\uE946", _localizer.Get("Settings.About.Title"), _localizer.Get("Settings.About.Description")),
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
        SelectedHorizontalPosition = settings.HorizontalPosition;
        SelectedVerticalPosition = settings.VerticalPosition;
        SelectedWindowPositionMode = settings.WindowPositionMode;
        HideShortcutResults = settings.HideShortcutResults;
        SearchDebounceEnabled = settings.SearchDebounceEnabled;
        SearchDebounceMillisecondsText = settings.SearchDebounceMilliseconds.ToString();
        LoggingEnabled = settings.LoggingEnabled;
        SelectedLogLevel = settings.MinLogLevel;

        LogLevelOptions =
        [
            new OptionItem<LogLevel> { Value = LogLevel.Trace, Label = "Trace" },
            new OptionItem<LogLevel> { Value = LogLevel.Debug, Label = "Debug" },
            new OptionItem<LogLevel> { Value = LogLevel.Info, Label = "Info" },
            new OptionItem<LogLevel> { Value = LogLevel.Warn, Label = "Warn" },
            new OptionItem<LogLevel> { Value = LogLevel.Error, Label = "Error" },
        ];

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

    public IReadOnlyList<OptionItem<SearchBarHorizontalPosition>> SearchBarHorizontalOptions { get; }

    public IReadOnlyList<OptionItem<SearchBarVerticalPosition>> SearchBarVerticalOptions { get; }

    public IReadOnlyList<OptionItem<SearchWindowPositionMode>> SearchWindowPositionOptions { get; }

    public bool IsPositionExpanded
    {
        get => _isPositionExpanded;
        set
        {
            if (_isPositionExpanded == value)
            {
                return;
            }

            _isPositionExpanded = value;
            OnPropertyChanged();
        }
    }

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

    public bool HideShortcutResults
    {
        get => _hideShortcutResults;
        set
        {
            if (_hideShortcutResults == value)
            {
                return;
            }

            _hideShortcutResults = value;
            OnPropertyChanged();
        }
    }

    public bool SearchDebounceEnabled
    {
        get => _searchDebounceEnabled;
        set
        {
            if (_searchDebounceEnabled == value)
            {
                return;
            }

            _searchDebounceEnabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SearchDebounceSettingsVisibility));
        }
    }

    public string SearchDebounceMillisecondsText
    {
        get => _searchDebounceMillisecondsText;
        set
        {
            if (string.Equals(_searchDebounceMillisecondsText, value, StringComparison.Ordinal))
            {
                return;
            }

            _searchDebounceMillisecondsText = value;
            OnPropertyChanged();
        }
    }

    public Visibility SearchDebounceSettingsVisibility => SearchDebounceEnabled ? Visibility.Visible : Visibility.Collapsed;

    public bool LoggingEnabled
    {
        get => _loggingEnabled;
        set
        {
            if (_loggingEnabled == value)
            {
                return;
            }

            _loggingEnabled = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<OptionItem<LogLevel>> LogLevelOptions { get; }

    public LogLevel SelectedLogLevel
    {
        get => _selectedLogLevel;
        set
        {
            if (_selectedLogLevel == value)
            {
                return;
            }

            _selectedLogLevel = value;
            LogService.Instance.MinLevel = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<LogEntry> LogEntries => LogService.Instance.Entries;

    public string AppDisplayName => "FastTools";

    public string AppVersion => AppInfo.Version;

    public string AppCopyright => AppInfo.Copyright;

    public string AppGitHubUrl => AppInfo.GitHubUrl;

    public SearchBarHorizontalPosition SelectedHorizontalPosition
    {
        get => _selectedHorizontalPosition;
        set
        {
            if (_selectedHorizontalPosition == value)
            {
                return;
            }

            _selectedHorizontalPosition = value;
            OnPropertyChanged();
        }
    }

    public SearchBarVerticalPosition SelectedVerticalPosition
    {
        get => _selectedVerticalPosition;
        set
        {
            if (_selectedVerticalPosition == value)
            {
                return;
            }

            _selectedVerticalPosition = value;
            OnPropertyChanged();
        }
    }

    public SearchWindowPositionMode SelectedWindowPositionMode
    {
        get => _selectedWindowPositionMode;
        set
        {
            if (_selectedWindowPositionMode == value)
            {
                return;
            }

            _selectedWindowPositionMode = value;
            OnPropertyChanged();
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

    public Visibility LogsSectionVisibility => GetSectionVisibility(SettingsSection.Logs);

    public Visibility AboutSectionVisibility => GetSectionVisibility(SettingsSection.About);

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
            if (value?.Section == SettingsSection.Logs)
            {
                ScheduleScrollLogsToBottom();
            }
            OnPropertyChanged(nameof(SelectedSectionTitle));
            OnPropertyChanged(nameof(SelectedSectionDescription));
            OnPropertyChanged(nameof(SelectedSectionHeaderVisibility));
            OnPropertyChanged(nameof(GeneralSectionVisibility));
            OnPropertyChanged(nameof(HotKeysSectionVisibility));
            OnPropertyChanged(nameof(CommandsSectionVisibility));
            OnPropertyChanged(nameof(PrioritySectionVisibility));
            OnPropertyChanged(nameof(PluginsSectionVisibility));
            OnPropertyChanged(nameof(LogsSectionVisibility));
            OnPropertyChanged(nameof(AboutSectionVisibility));
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
        RebuildLogDocument();
        LogService.Instance.Entries.CollectionChanged += LogEntries_CollectionChanged;
        Closed += (_, _) => LogService.Instance.Entries.CollectionChanged -= LogEntries_CollectionChanged;
        ScheduleScrollLogsToBottom();
    }

    private void LogEntries_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (LogTextBox is null)
        {
            return;
        }

        switch (e.Action)
        {
            case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                if (e.NewItems is not null)
                {
                    foreach (var item in e.NewItems)
                    {
                        if (item is LogEntry entry)
                        {
                            AppendLogParagraph(entry);
                        }
                    }
                }
                ScheduleScrollLogsToBottom();
                break;

            case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                if (LogTextBox.Document.Blocks.FirstBlock is { } first)
                {
                    LogTextBox.Document.Blocks.Remove(first);
                }
                break;

            case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                LogTextBox.Document.Blocks.Clear();
                break;
        }
    }

    private void RebuildLogDocument()
    {
        if (LogTextBox is null)
        {
            return;
        }

        LogTextBox.Document.Blocks.Clear();
        foreach (var entry in LogService.Instance.Entries)
        {
            AppendLogParagraph(entry);
        }
    }

    private void AppendLogParagraph(LogEntry entry)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 1, 0, 1),
            LineHeight = double.NaN,
        };

        foreach (var run in ParseMarkup(entry.Markup))
        {
            paragraph.Inlines.Add(run);
        }

        LogTextBox!.Document.Blocks.Add(paragraph);
    }

    private static IEnumerable<Run> ParseMarkup(string markup)
    {
        // Tokens: [#RGB] or [#RRGGBB] or [#AARRGGBB] ... [/]
        var matches = System.Text.RegularExpressions.Regex.Matches(
            markup, @"\[#(?<hex>[0-9A-Fa-f]{3,8})\](?<text>.*?)\[/\]",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        var cursor = 0;
        foreach (System.Text.RegularExpressions.Match m in matches)
        {
            if (m.Index > cursor)
            {
                yield return new Run(markup.Substring(cursor, m.Index - cursor));
            }

            var hex = "#" + m.Groups["hex"].Value;
            var text = m.Groups["text"].Value;
            var run = new Run(text) { FontWeight = FontWeights.SemiBold };
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!;
                run.Foreground = new SolidColorBrush(color);
            }
            catch
            {
                // ignore invalid color, keep default foreground
            }
            yield return run;

            cursor = m.Index + m.Length;
        }

        if (cursor < markup.Length)
        {
            yield return new Run(markup.Substring(cursor));
        }
    }

    private void ScheduleScrollLogsToBottom()
    {
        Dispatcher.BeginInvoke(
            () =>
            {
                if (LogTextBox is null)
                {
                    return;
                }
                LogTextBox.UpdateLayout();
                LogTextBox.ScrollToEnd();
            },
            System.Windows.Threading.DispatcherPriority.ContextIdle);
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

    private void SearchDebounceMillisecondsTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = string.IsNullOrEmpty(e.Text) || e.Text.Any(character => !char.IsDigit(character));
    }

    private void SearchDebounceMillisecondsTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(System.Windows.DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        if (e.DataObject.GetData(System.Windows.DataFormats.Text) is not string text || text.Any(character => !char.IsDigit(character)))
        {
            e.CancelCommand();
        }
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

        if (SearchDebounceEnabled &&
            (!int.TryParse(SearchDebounceMillisecondsText, out var searchDebounceMilliseconds) || searchDebounceMilliseconds < 0))
        {
            System.Windows.MessageBox.Show(
                _localizer.Get("Settings.InvalidSearchDebounceMilliseconds"),
                _localizer.Get("Settings.Title"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        var validatedSearchDebounceMilliseconds = int.TryParse(SearchDebounceMillisecondsText, out var parsedSearchDebounceMilliseconds) && parsedSearchDebounceMilliseconds >= 0
            ? parsedSearchDebounceMilliseconds
            : LauncherSettings.DefaultSearchDebounceMilliseconds;

        SavedSettings = _baseSettings.Clone();
        SavedSettings.HotKey = HotKeyText;
        SavedSettings.ThemeMode = SelectedTheme;
        SavedSettings.Language = SelectedLanguage;
        SavedSettings.HorizontalPosition = SelectedHorizontalPosition;
        SavedSettings.VerticalPosition = SelectedVerticalPosition;
        SavedSettings.WindowPositionMode = SelectedWindowPositionMode;
        SavedSettings.HideShortcutResults = HideShortcutResults;
        SavedSettings.SearchDebounceEnabled = SearchDebounceEnabled;
        SavedSettings.SearchDebounceMilliseconds = validatedSearchDebounceMilliseconds;
        SavedSettings.LoggingEnabled = LoggingEnabled;
        SavedSettings.MinLogLevel = SelectedLogLevel;
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

    private void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        LogService.Instance.Clear();
    }

    private void LogBox_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
    {
        // Prevent the outer settings ScrollViewer from scrolling when clicking inside the log box,
        // which would push the level dropdown off-screen.
        e.Handled = true;
    }

    private void CopyAllLogs_Click(object sender, RoutedEventArgs e)
    {
        var snapshot = LogService.Instance.Entries.ToArray();
        if (snapshot.Length == 0)
        {
            return;
        }

        var sb = new System.Text.StringBuilder();
        foreach (var entry in snapshot)
        {
            sb.Append(entry.FormattedTimestamp)
              .Append(' ')
              .Append(entry.LevelText)
              .Append(" [")
              .Append(entry.Source)
              .Append("] ")
              .AppendLine(entry.Message);
        }

        TryCopyToClipboard(sb.ToString());
    }

    private static void TryCopyToClipboard(string text)
    {
        try
        {
            System.Windows.Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            LogService.Instance.WarnKey("Settings", "Log.Settings.CopyLogsFailed", ex.Message);
        }
    }

    private void OpenGitHub_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppInfo.GitHubUrl,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            LogService.Instance.WarnKey("Settings", "Log.Settings.OpenGitHubFailed", ex.Message);
        }
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
        Sections.Add(new SettingsSectionItem(SettingsSection.Logs, "\uE9F9", _localizer.Get("Settings.Logs.Title"), _localizer.Get("Settings.Logs.Description")));
        Sections.Add(new SettingsSectionItem(SettingsSection.About, "\uE946", _localizer.Get("Settings.About.Title"), _localizer.Get("Settings.About.Description")));
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

        foreach (var option in SearchBarHorizontalOptions)
        {
            option.Label = option.Value switch
            {
                SearchBarHorizontalPosition.Left => _localizer.Get("SearchBarPosition.Horizontal.Left"),
                SearchBarHorizontalPosition.Center => _localizer.Get("SearchBarPosition.Horizontal.Center"),
                SearchBarHorizontalPosition.Right => _localizer.Get("SearchBarPosition.Horizontal.Right"),
                _ => option.Label,
            };
        }

        foreach (var option in SearchBarVerticalOptions)
        {
            option.Label = option.Value switch
            {
                SearchBarVerticalPosition.Top => _localizer.Get("SearchBarPosition.Vertical.Top"),
                SearchBarVerticalPosition.Middle => _localizer.Get("SearchBarPosition.Vertical.Middle"),
                _ => option.Label,
            };
        }

        foreach (var option in SearchWindowPositionOptions)
        {
            option.Label = option.Value switch
            {
                SearchWindowPositionMode.RememberLast => _localizer.Get("WindowPosition.RememberLast"),
                SearchWindowPositionMode.FollowMouse => _localizer.Get("WindowPosition.FollowMouse"),
                SearchWindowPositionMode.PrimaryMonitor => _localizer.Get("WindowPosition.PrimaryMonitor"),
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
        if (CommandNameColumn is not null)
        {
            CommandNameColumn.Header = _localizer.Get("Settings.Name");
        }

        if (CommandAliasColumn is not null)
        {
            CommandAliasColumn.Header = _localizer.Get("Settings.Alias");
        }

        if (CommandPathColumn is not null)
        {
            CommandPathColumn.Header = _localizer.Get("Settings.Command");
        }

        if (CommandArgsColumn is not null)
        {
            CommandArgsColumn.Header = _localizer.Get("Settings.Arguments");
        }

        if (CommandConfirmColumn is not null)
        {
            CommandConfirmColumn.Header = _localizer.Get("Settings.Confirm");
        }
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
        Logs,
        About,
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

}
