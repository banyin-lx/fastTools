using FastTools.App.Models;
using System.Windows.Media;

namespace FastTools.App.Services;

public sealed class ThemeService
{
    private readonly System.Windows.Application _application;

    public ThemeService(System.Windows.Application application)
    {
        _application = application;
    }

    public AppThemeMode CurrentTheme { get; private set; } = AppThemeMode.Dark;

    public void Apply(AppThemeMode themeMode)
    {
        CurrentTheme = themeMode;
        var palette = themeMode == AppThemeMode.Dark ? BuildDarkPalette() : BuildLightPalette();

        foreach (var entry in palette)
        {
            _application.Resources[entry.Key] = entry.Value;
        }
    }

    private static Dictionary<string, object> BuildDarkPalette()
    {
        return new Dictionary<string, object>
        {
            ["AppBackgroundBrush"] = CreateBrush("#08131F"),
            ["ChromeBrush"] = CreateBrush("#102538"),
            ["CardBrush"] = CreateBrush("#132A40"),
            ["ElevatedBrush"] = CreateBrush("#17324A"),
            ["BorderBrush"] = CreateBrush("#284661"),
            ["TextPrimaryBrush"] = CreateBrush("#F3F7FB"),
            ["TextSecondaryBrush"] = CreateBrush("#A9C0D3"),
            ["AccentBrush"] = CreateBrush("#7CE0B8"),
            ["AccentStrongBrush"] = CreateBrush("#2FD39A"),
            ["SelectionBrush"] = CreateBrush("#1F425E"),
            ["DangerBrush"] = CreateBrush("#F46D6D"),
        };
    }

    private static Dictionary<string, object> BuildLightPalette()
    {
        return new Dictionary<string, object>
        {
            ["AppBackgroundBrush"] = CreateBrush("#EEF4F8"),
            ["ChromeBrush"] = CreateBrush("#FFFFFF"),
            ["CardBrush"] = CreateBrush("#F7FBFD"),
            ["ElevatedBrush"] = CreateBrush("#FFFFFF"),
            ["BorderBrush"] = CreateBrush("#D5E2EC"),
            ["TextPrimaryBrush"] = CreateBrush("#0D2436"),
            ["TextSecondaryBrush"] = CreateBrush("#5A7184"),
            ["AccentBrush"] = CreateBrush("#0F9A77"),
            ["AccentStrongBrush"] = CreateBrush("#08795D"),
            ["SelectionBrush"] = CreateBrush("#DCEFF0"),
            ["DangerBrush"] = CreateBrush("#D94A4A"),
        };
    }

    private static SolidColorBrush CreateBrush(string value)
    {
        var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value));
        brush.Freeze();
        return brush;
    }
}
