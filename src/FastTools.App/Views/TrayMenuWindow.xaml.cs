using FastTools.App.Services;
using System.Windows;
using System.Windows.Input;

namespace FastTools.App.Views;

public partial class TrayMenuWindow : Window
{
    private readonly Action _openAction;
    private readonly Action _settingsAction;
    private readonly Action _refreshAction;
    private readonly Action _exitAction;

    public TrayMenuWindow(
        LocalizationService localizer,
        Action openAction,
        Action settingsAction,
        Action refreshAction,
        Action exitAction)
    {
        _openAction = openAction;
        _settingsAction = settingsAction;
        _refreshAction = refreshAction;
        _exitAction = exitAction;

        InitializeComponent();
        DataContext = localizer;
        Deactivated += TrayMenuWindow_Deactivated;
        PreviewKeyDown += TrayMenuWindow_PreviewKeyDown;
    }

    private void TrayMenuWindow_Deactivated(object? sender, EventArgs e)
    {
        if (!IsLoaded || Dispatcher.HasShutdownStarted)
        {
            return;
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (IsLoaded)
            {
                Close();
            }
        }));
    }

    private void TrayMenuWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        Close();
        e.Handled = true;
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        _openAction();
        Close();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _settingsAction();
        Close();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        _refreshAction();
        Close();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        _exitAction();
        Close();
    }
}
