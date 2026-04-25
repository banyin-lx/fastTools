using System.IO;
using FastTools.App.Providers;
using FastTools.App.Services;
using System.Windows;

namespace FastTools.App;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceName = "FastTools.Launcher.Singleton";
    private const string ShowMessageName = "FastTools.Launcher.ShowMessage";

    private Mutex? _singleInstanceMutex;
    private TrayIconService? _trayIconService;
    private LocalizationService _localizationService = null!;

    public LauncherSettingsStore SettingsStore { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += App_DispatcherUnhandledException;

        var createdNew = false;
        _singleInstanceMutex = new Mutex(true, SingleInstanceName, out createdNew);
        var showMessageId = NativeMethods.RegisterWindowMessage(ShowMessageName);

        if (!createdNew)
        {
            NativeMethods.PostMessage((IntPtr)NativeMethods.HwndBroadcast, showMessageId, IntPtr.Zero, IntPtr.Zero);
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        SettingsStore = new LauncherSettingsStore();
        await SettingsStore.LoadAsync();
        _localizationService = new LocalizationService();
        _localizationService.Apply(SettingsStore.Current.Language);

        var themeService = new ThemeService(this);
        themeService.Apply(SettingsStore.Current.ThemeMode);

        var appIndexService = new ApplicationIndexService(SettingsStore);
        var fileIndexService = new FileIndexService(SettingsStore);
        var pluginHostService = new PluginHostService(SettingsStore);
        await pluginHostService.LoadAsync();
        await SettingsStore.SaveAsync(SettingsStore.Current);

        var searchService = new SearchService(
        [
            new ApplicationSearchProvider(appIndexService),
            new FileSearchProvider(fileIndexService),
            new CustomCommandSearchProvider(SettingsStore),
            new PluginSearchProvider(pluginHostService),
        ], SettingsStore, _localizationService);

        var mainWindow = new MainWindow(
            showMessageId,
            new GlobalHotKeyService(),
            searchService,
            SettingsStore,
            _localizationService,
            themeService,
            appIndexService,
            fileIndexService,
            pluginHostService);

        _trayIconService = new TrayIconService();
        _trayIconService.ApplyLocalization(_localizationService);
        _trayIconService.OpenRequested += (_, _) => Dispatcher.Invoke(mainWindow.ShowLauncher);
        _trayIconService.SettingsRequested += async (_, _) => await Dispatcher.InvokeAsync(async () => await mainWindow.OpenSettingsFromTrayAsync()).Task.Unwrap();
        _trayIconService.RefreshRequested += async (_, _) => await Dispatcher.InvokeAsync(async () => await mainWindow.RefreshIndexesFromTrayAsync()).Task.Unwrap();
        _trayIconService.ExitRequested += (_, _) => Dispatcher.Invoke(Shutdown);

        MainWindow = mainWindow;
        mainWindow.InitializeShell(_trayIconService);
        mainWindow.HideLauncher();

        _ = Task.Run(appIndexService.RefreshAsync);
        _ = Task.Run(fileIndexService.RefreshAsync);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIconService?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FastTools");
            Directory.CreateDirectory(appDataPath);
            var logPath = Path.Combine(appDataPath, "error.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception}\r\n\r\n");
            _trayIconService?.ShowBalloonTip("FastTools", e.Exception.Message, System.Windows.Forms.ToolTipIcon.Warning);
        }
        catch
        {
        }

        e.Handled = true;
    }
}
