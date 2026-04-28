using FastTools.App.Models;
using FastTools.App.Services;
using FastTools.App.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace FastTools.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly uint _showWindowMessageId;
    private readonly GlobalHotKeyService _hotKeyService;
    private readonly SearchService _searchService;
    private readonly LauncherSettingsStore _settingsStore;
    private readonly LocalizationService _localizer;
    private readonly ThemeService _themeService;
    private readonly ApplicationIndexService _applicationIndexService;
    private readonly PluginHostService _pluginHostService;
    private double _lastWindowLeft;
    private double _lastWindowTop;

    private TrayIconService? _trayIconService;
    private CancellationTokenSource? _searchCancellationTokenSource;
    private bool _isHotKeyRegistered;
    private bool _hasQuery;
    private bool _nativeInteropInitialized;
    private bool _suspendAutoHide;
    private int _searchVersion;
    private SearchResultItem? _selectedResult;

    public MainWindow(
        uint showWindowMessageId,
        GlobalHotKeyService hotKeyService,
        SearchService searchService,
        LauncherSettingsStore settingsStore,
        LocalizationService localizer,
        ThemeService themeService,
        ApplicationIndexService applicationIndexService,
        PluginHostService pluginHostService)
    {
        _showWindowMessageId = showWindowMessageId;
        _hotKeyService = hotKeyService;
        _searchService = searchService;
        _settingsStore = settingsStore;
        _localizer = localizer;
        _themeService = themeService;
        _applicationIndexService = applicationIndexService;
        _pluginHostService = pluginHostService;

        InitializeComponent();
        DataContext = this;

        Results = [];
        ResultsView = CollectionViewSource.GetDefaultView(Results);
        ResultsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SearchResultItem.DisplayGroup)));

        SelectedActions = [];
        ResultsVisibility = Visibility.Collapsed;
        EmptyStateVisibility = Visibility.Collapsed;
        ResultsListVisibility = Visibility.Collapsed;

        Deactivated += MainWindow_Deactivated;
        Closing += MainWindow_Closing;
        _hotKeyService.HotKeyPressed += MainHotKey_Pressed;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SearchResultItem> Results { get; }

    public ObservableCollection<SearchAction> SelectedActions { get; }

    public ICollectionView ResultsView { get; }

    public LocalizationService Localizer => _localizer;

    public Visibility ResultsVisibility
    {
        get;
        private set;
    }

    public Visibility EmptyStateVisibility
    {
        get;
        private set;
    }

    public Visibility ResultsListVisibility
    {
        get;
        private set;
    }

    public SearchResultItem? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (_selectedResult == value)
            {
                return;
            }

            _selectedResult = value;
            OnPropertyChanged();
            RefreshSelectedActions();
        }
    }

    public bool InitializeShell(TrayIconService trayIconService)
    {
        _trayIconService = trayIconService;
        EnsureNativeInteropInitialized();
        return TryRegisterHotKey(showErrorDialog: true);
    }

    public void ShowLauncher()
    {
        ToggleVisibility(forceShow: true);
    }

    public Task OpenSettingsFromTrayAsync()
    {
        return OpenSettingsAsync();
    }

    public Task RefreshIndexesFromTrayAsync()
    {
        ShowLauncher();
        return RefreshIndexesAsync();
    }

    public void HideLauncher()
    {
        // Save current position for RememberLast mode
        if (_settingsStore.Current.WindowPositionMode == SearchWindowPositionMode.RememberLast)
        {
            _lastWindowLeft = Left;
            _lastWindowTop = Top;
        }

        ResetTransientState();

        if (!_isHotKeyRegistered)
        {
            ShowInTaskbar = false;
            Hide();
            return;
        }

        Hide();
        ShowInTaskbar = false;
    }

    private void EnsureNativeInteropInitialized()
    {
        if (_nativeInteropInitialized)
        {
            return;
        }

        var handle = new WindowInteropHelper(this).EnsureHandle();
        var source = HwndSource.FromHwnd(handle);
        source?.AddHook(WndProc);
        _nativeInteropInitialized = true;
    }

    private bool TryRegisterHotKey(bool showErrorDialog, bool logOutcome = true)
    {
        EnsureNativeInteropInitialized();

        var handle = new WindowInteropHelper(this).Handle;
        var gesture = _settingsStore.Current.HotKey;
        if (_hotKeyService.Register(handle, gesture, out var errorMessage))
        {
            _isHotKeyRegistered = true;
            ShowInTaskbar = false;
            if (logOutcome)
            {
                FastTools.App.Services.LogService.Instance.InfoKey(
                    "HotKey",
                    "Log.HotKey.Registered",
                    gesture);
            }

            return true;
        }

        _isHotKeyRegistered = false;
        ShowInTaskbar = false;
        var localizedError = string.IsNullOrEmpty(errorMessage)
            ? string.Empty
            : _localizer.Get(errorMessage);
        if (logOutcome)
        {
            FastTools.App.Services.LogService.Instance.ErrorKey(
                "HotKey",
                "Log.HotKey.RegisterFailed",
                gesture, localizedError);
        }

        if (showErrorDialog)
        {
            _trayIconService?.ShowBalloonTip(
                _localizer.Get("Main.HotKeyError.Title"),
                $"{localizedError} {_localizer.Get("Main.HotKeyError.Body")}",
                System.Windows.Forms.ToolTipIcon.Warning);
        }

        return false;
    }

    private void MainHotKey_Pressed(object? sender, EventArgs e)
    {
        if (_suspendAutoHide)
        {
            return;
        }

        ToggleVisibility();
    }

    private void MainWindow_Deactivated(object? sender, EventArgs e)
    {
        if (_suspendAutoHide || !_isHotKeyRegistered)
        {
            return;
        }

        if (IsVisible)
        {
            HideLauncher();
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        _hotKeyService.Dispose();
    }

    private async void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _hasQuery = !string.IsNullOrWhiteSpace(SearchBox.Text);
        SearchPlaceholder.Visibility = _hasQuery ? Visibility.Collapsed : Visibility.Visible;
        UpdateWindowPresentation();

        await SearchAsync(SearchBox.Text);
    }

    private async void SearchBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            MoveSelection(1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            MoveSelection(-1);
            e.Handled = true;
            return;
        }

        if (MatchesResultActivationGesture(e))
        {
            await ExecuteSelectedAsync(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            HideLauncher();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.OemComma && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            await OpenSettingsAsync();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.C &&
            Keyboard.Modifiers.HasFlag(ModifierKeys.Control) &&
            SelectedActions.FirstOrDefault(action => action.Label == "Copy Path") is { } copyAction)
        {
            await copyAction.ExecuteAsync();
            e.Handled = true;
        }
    }

    private async void ResultsList_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_settingsStore.Current.ResultMouseActivationMode != ResultMouseActivationMode.DoubleClick)
        {
            return;
        }

        await ExecuteSelectedAsync(false);
    }

    private async void ResultsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_settingsStore.Current.ResultMouseActivationMode != ResultMouseActivationMode.SingleClick)
        {
            return;
        }

        if (FindResultItemFromSource(e.OriginalSource as DependencyObject) is not SearchResultItem item)
        {
            return;
        }

        if (!ReferenceEquals(SelectedResult, item))
        {
            SelectedResult = item;
        }

        await ExecuteSelectedAsync(false);
        e.Handled = true;
    }

    private async void ResultsList_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_settingsStore.Current.ResultMouseActivationMode != ResultMouseActivationMode.RightClick)
        {
            return;
        }

        if (FindResultItemFromSource(e.OriginalSource as DependencyObject) is not SearchResultItem item)
        {
            return;
        }

        if (!ReferenceEquals(SelectedResult, item))
        {
            SelectedResult = item;
        }

        await ExecuteSelectedAsync(false);
        e.Handled = true;
    }

    private async Task SearchAsync(string query)
    {
        var normalizedQuery = query.Trim();
        var currentSearchVersion = Interlocked.Increment(ref _searchVersion);

        _searchCancellationTokenSource?.Cancel();
        _searchCancellationTokenSource?.Dispose();
        _searchCancellationTokenSource = null;

        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            Results.Clear();
            SelectedResult = null;
            EmptyStateVisibility = Visibility.Collapsed;
            ResultsListVisibility = Visibility.Collapsed;
            OnPropertyChanged(nameof(EmptyStateVisibility));
            OnPropertyChanged(nameof(ResultsListVisibility));
            UpdateWindowPresentation();
            return;
        }

        _searchCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _searchCancellationTokenSource.Token;

        try
        {
            if (_settingsStore.Current.SearchDebounceEnabled)
            {
                await Task.Delay(_settingsStore.Current.SearchDebounceMilliseconds, cancellationToken);
            }

            FastTools.App.Services.LogService.Instance.DebugKey("Search", "Log.Search.Query", normalizedQuery);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var results = await _searchService.SearchAsync(normalizedQuery, cancellationToken);
            sw.Stop();
            if (cancellationToken.IsCancellationRequested || currentSearchVersion != _searchVersion)
            {
                return;
            }

            Results.Clear();
            foreach (var result in results)
            {
                Results.Add(result);
            }

            SelectedResult = Results.FirstOrDefault();
            ResultsListVisibility = Results.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyStateVisibility = Visibility.Collapsed;
            OnPropertyChanged(nameof(ResultsListVisibility));
            OnPropertyChanged(nameof(EmptyStateVisibility));
            UpdateWindowPresentation();
            FastTools.App.Services.LogService.Instance.InfoKey("Search", "Log.Search.Result", normalizedQuery, results.Count, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            FastTools.App.Services.LogService.Instance.ErrorKey("Search", "Log.Search.Failed", normalizedQuery, exception.Message);
            if (currentSearchVersion != _searchVersion)
            {
                return;
            }

            Results.Clear();
            SelectedResult = null;
            EmptyStateVisibility = Visibility.Collapsed;
            ResultsListVisibility = Visibility.Collapsed;
            OnPropertyChanged(nameof(EmptyStateVisibility));
            OnPropertyChanged(nameof(ResultsListVisibility));
            UpdateWindowPresentation();
            _trayIconService?.ShowBalloonTip(
                _localizer.Get("Main.HotKeyError.Title"),
                exception.Message,
                System.Windows.Forms.ToolTipIcon.Warning);
        }
    }

    private void MoveSelection(int delta)
    {
        if (Results.Count == 0)
        {
            return;
        }

        var currentIndex = SelectedResult is null ? -1 : Results.IndexOf(SelectedResult);
        var nextIndex = currentIndex + delta;

        if (nextIndex < 0)
        {
            nextIndex = Results.Count - 1;
        }
        else if (nextIndex >= Results.Count)
        {
            nextIndex = 0;
        }

        SelectedResult = Results[nextIndex];
        ResultsList.ScrollIntoView(SelectedResult);
    }

    private bool MatchesResultActivationGesture(System.Windows.Input.KeyEventArgs e)
    {
        if (!HotKeyGesture.TryParse(_settingsStore.Current.ResultKeyboardActivationGesture, out var gesture) || gesture is null)
        {
            return e.Key == Key.Enter;
        }

        var actualKey = GetActualKey(e);
        return actualKey == gesture.Key && Keyboard.Modifiers == gesture.Modifiers;
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

    private static SearchResultItem? FindResultItemFromSource(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { DataContext: SearchResultItem item })
            {
                return item;
            }

            source = source is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        return null;
    }

    private async Task ExecuteSelectedAsync(bool runAsAdmin)
    {
        var selectedResult = SelectedResult;
        if (selectedResult is null)
        {
            return;
        }

        if (selectedResult.RequiresConfirmation)
        {
            _suspendAutoHide = true;
            try
            {
                var result = System.Windows.MessageBox.Show(
                    selectedResult.ConfirmationMessage ?? _localizer.Get("Main.ActionConfirm"),
                    _localizer.Get("Main.HotKeyError.Title"),
                    System.Windows.MessageBoxButton.OKCancel,
                    System.Windows.MessageBoxImage.Warning);
                if (result != System.Windows.MessageBoxResult.OK)
                {
                    return;
                }
            }
            finally
            {
                _suspendAutoHide = false;
            }
        }

        try
        {
            if (runAsAdmin && selectedResult.SupportsRunAsAdmin && selectedResult.ExecuteAsAdminAsync is not null)
            {
                await selectedResult.ExecuteAsAdminAsync(CancellationToken.None);
            }
            else
            {
                await selectedResult.ExecuteAsync(CancellationToken.None);
            }

            await _searchService.RecordUsageAsync(selectedResult);
            HideLauncher();
        }
        catch (Exception exception)
        {
            _trayIconService?.ShowBalloonTip(_localizer.Get("Main.HotKeyError.Title"), exception.Message, System.Windows.Forms.ToolTipIcon.Warning);
        }
    }

    private async Task RefreshIndexesAsync()
    {
        await _applicationIndexService.RefreshAsync();
        await _pluginHostService.LoadAsync();
        await _settingsStore.SaveAsync(_settingsStore.Current);
        await SearchAsync(SearchBox.Text);
    }

    private async Task OpenSettingsAsync()
    {
        _suspendAutoHide = true;
        _hotKeyService.Unregister();
        _isHotKeyRegistered = false;
        var settingsConfirmed = false;

        try
        {
            HideLauncher();

            var settingsWindow = new SettingsWindow(_settingsStore.Current.Clone(), _pluginHostService.Plugins, _localizer, _themeService);
            if (IsLoaded)
            {
                settingsWindow.Owner = this;
            }

            if (settingsWindow.ShowDialog() != true || settingsWindow.SavedSettings is null)
            {
                return;
            }

            settingsConfirmed = true;
            await _settingsStore.SaveAsync(settingsWindow.SavedSettings);
            // Reset cached position so new horizontal/vertical settings take effect on next show
            _lastWindowLeft = 0;
            _lastWindowTop = 0;
            _localizer.Apply(_settingsStore.Current.Language);
            _trayIconService?.ApplyLocalization(_localizer);
            _themeService.Apply(_settingsStore.Current.ThemeMode);
            await _pluginHostService.LoadAsync();
            await _applicationIndexService.RefreshAsync();

            await SearchAsync(SearchBox.Text);
        }
        catch (Exception exception)
        {
            _trayIconService?.ShowBalloonTip(
                _localizer.Get("Main.HotKeyError.Title"),
                exception.Message,
                System.Windows.Forms.ToolTipIcon.Warning);
        }
        finally
        {
            TryRegisterHotKey(showErrorDialog: settingsConfirmed, logOutcome: settingsConfirmed);
            _suspendAutoHide = false;
        }
    }

    private void RefreshSelectedActions()
    {
        SelectedActions.Clear();
        foreach (var action in SelectedResult?.Actions ?? [])
        {
            SelectedActions.Add(action);
        }
    }

    private void ToggleVisibility(bool forceShow = false)
    {
        if (!_isHotKeyRegistered)
        {
            PositionLauncher();
            Show();
            WindowState = WindowState.Normal;
            ShowInTaskbar = false;
            Activate();
            SearchBox.Focus();
            SearchBox.SelectAll();
            return;
        }

        if (IsVisible && !forceShow)
        {
            HideLauncher();
            return;
        }

        PositionLauncher();
        Show();
        ShowInTaskbar = false;
        Activate();
        Topmost = true;
        Topmost = false;
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void ResetTransientState()
    {
        _hasQuery = false;
        SearchBox.Text = string.Empty;
        SearchPlaceholder.Visibility = Visibility.Visible;
        Results.Clear();
        SelectedActions.Clear();
        SelectedResult = null;
        ResultsVisibility = Visibility.Collapsed;
        EmptyStateVisibility = Visibility.Collapsed;
        ResultsListVisibility = Visibility.Collapsed;
        UpdateWindowPresentation();
        OnPropertyChanged(nameof(ResultsVisibility));
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(ResultsListVisibility));
    }

    private void UpdateWindowPresentation()
    {
        var shouldShowResults = Results.Count > 0;
        var wasVisible = ResultsVisibility == Visibility.Visible;

        ResultsVisibility = shouldShowResults ? Visibility.Visible : Visibility.Collapsed;
        OnPropertyChanged(nameof(ResultsVisibility));

        if (shouldShowResults && !wasVisible)
        {
            BeginResultsPanelAnimation();
        }
        else if (!shouldShowResults)
        {
            ResultsPanel.BeginAnimation(OpacityProperty, null);
            ResultsPanelTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);
            ResultsPanel.Opacity = 0;
            ResultsPanelTranslate.Y = 10;
        }
    }

    private void PositionLauncher()
    {
        var settings = _settingsStore.Current;
        var workArea = SystemParameters.WorkArea;
        var width = Width;
        var height = ActualHeight > 0 ? ActualHeight : 96;

        if (settings.WindowPositionMode == SearchWindowPositionMode.FollowMouse)
        {
            var cursorPos = System.Windows.Forms.Cursor.Position;
            var screen = System.Windows.Forms.Screen.FromPoint(cursorPos);
            var monitorArea = new System.Windows.Rect(
                screen.WorkingArea.X, screen.WorkingArea.Y,
                screen.WorkingArea.Width, screen.WorkingArea.Height);

            Left = settings.HorizontalPosition switch
            {
                SearchBarHorizontalPosition.Left => monitorArea.Left,
                SearchBarHorizontalPosition.Right => monitorArea.Left + monitorArea.Width - width,
                _ => monitorArea.Left + (monitorArea.Width - width) / 2d,
            };
            Top = settings.VerticalPosition switch
            {
                SearchBarVerticalPosition.Middle => monitorArea.Top + (monitorArea.Height - height) / 2d,
                _ => monitorArea.Top + Math.Max(48d, (monitorArea.Height - height) / 4.2d),
            };
            return;
        }

        if (settings.WindowPositionMode == SearchWindowPositionMode.RememberLast &&
            (_lastWindowLeft != 0 || _lastWindowTop != 0))
        {
            Left = _lastWindowLeft;
            Top = _lastWindowTop;
            return;
        }

        // PrimaryMonitor or first launch: position based on Horizontal/Vertical settings
        Left = settings.HorizontalPosition switch
        {
            SearchBarHorizontalPosition.Left => workArea.Left,
            SearchBarHorizontalPosition.Right => workArea.Left + workArea.Width - width,
            _ => workArea.Left + (workArea.Width - width) / 2d,
        };

        Top = settings.VerticalPosition switch
        {
            SearchBarVerticalPosition.Middle => workArea.Top + (workArea.Height - height) / 2d,
            _ => workArea.Top + Math.Max(48d, (workArea.Height - height) / 4.2d),
        };
    }

    private void ChromeBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (IsClickFromInteractiveElement(e.OriginalSource as DependencyObject))
        {
            return;
        }

        DragMove();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((uint)msg == _showWindowMessageId)
        {
            PositionLauncher();
            Show();
            WindowState = WindowState.Normal;
            NativeMethods.ShowWindow(hwnd, NativeMethods.SwShow);
            NativeMethods.SetForegroundWindow(hwnd);
            Activate();
            SearchBox.Focus();
            handled = true;
            return IntPtr.Zero;
        }

        handled = _hotKeyService.HandleMessage(msg, wParam);
        return IntPtr.Zero;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static bool IsClickFromInteractiveElement(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.TextBox
                or System.Windows.Controls.Button
                or System.Windows.Controls.ListBox
                or System.Windows.Controls.ListBoxItem
                or System.Windows.Controls.Primitives.ScrollBar)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void BeginResultsPanelAnimation()
    {
        ResultsPanel.Opacity = 0;
        ResultsPanelTranslate.Y = 10;

        var duration = TimeSpan.FromMilliseconds(180);
        var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };

        var opacityAnimation = new DoubleAnimation(0, 1, duration)
        {
            EasingFunction = easing,
        };

        var translateAnimation = new DoubleAnimation(10, 0, duration)
        {
            EasingFunction = easing,
        };

        ResultsPanel.BeginAnimation(OpacityProperty, opacityAnimation);
        ResultsPanelTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, translateAnimation);
    }
}
