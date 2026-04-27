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
        var palette = themeMode switch
        {
            AppThemeMode.Light => BuildLightPalette(),
            AppThemeMode.Soft => BuildSoftPalette(),
            AppThemeMode.Midnight => BuildMidnightPalette(),
            AppThemeMode.Sepia => BuildSepiaPalette(),
            _ => BuildDarkPalette(),
        };

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
            ["ScrollTrackBrush"] = CreateBrush("#113047"),
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
            ["ScrollTrackBrush"] = CreateBrush("#E2ECF2"),
        };
    }

    private static Dictionary<string, object> BuildSoftPalette()
    {
        return new Dictionary<string, object>
        {
            ["AppBackgroundBrush"] = CreateBrush("#F5EFE9"),
            ["ChromeBrush"] = CreateBrush("#FBF6F1"),
            ["CardBrush"] = CreateBrush("#FFFBF6"),
            ["ElevatedBrush"] = CreateBrush("#F1E7DC"),
            ["BorderBrush"] = CreateBrush("#E2D3C2"),
            ["TextPrimaryBrush"] = CreateBrush("#3A2E26"),
            ["TextSecondaryBrush"] = CreateBrush("#7B6857"),
            ["AccentBrush"] = CreateBrush("#C7896C"),
            ["AccentStrongBrush"] = CreateBrush("#A56A4F"),
            ["SelectionBrush"] = CreateBrush("#EDDDCD"),
            ["DangerBrush"] = CreateBrush("#C1564E"),
            ["ScrollTrackBrush"] = CreateBrush("#E9DCCD"),
        };
    }

    private static Dictionary<string, object> BuildMidnightPalette()
    {
        return new Dictionary<string, object>
        {
            ["AppBackgroundBrush"] = CreateBrush("#04060F"),
            ["ChromeBrush"] = CreateBrush("#0B1024"),
            ["CardBrush"] = CreateBrush("#0F162E"),
            ["ElevatedBrush"] = CreateBrush("#161E3D"),
            ["BorderBrush"] = CreateBrush("#22305C"),
            ["TextPrimaryBrush"] = CreateBrush("#EEF1FF"),
            ["TextSecondaryBrush"] = CreateBrush("#9AA5C9"),
            ["AccentBrush"] = CreateBrush("#7C9BFF"),
            ["AccentStrongBrush"] = CreateBrush("#4F71E0"),
            ["SelectionBrush"] = CreateBrush("#1E2A55"),
            ["DangerBrush"] = CreateBrush("#FF6F8C"),
            ["ScrollTrackBrush"] = CreateBrush("#0E1530"),
        };
    }

    private static Dictionary<string, object> BuildSepiaPalette()
    {
        return new Dictionary<string, object>
        {
            ["AppBackgroundBrush"] = CreateBrush("#F4ECD8"),
            ["ChromeBrush"] = CreateBrush("#FBF5E4"),
            ["CardBrush"] = CreateBrush("#FFF9E8"),
            ["ElevatedBrush"] = CreateBrush("#EFE3C7"),
            ["BorderBrush"] = CreateBrush("#D9C7A2"),
            ["TextPrimaryBrush"] = CreateBrush("#3E2C12"),
            ["TextSecondaryBrush"] = CreateBrush("#7A6440"),
            ["AccentBrush"] = CreateBrush("#A0682B"),
            ["AccentStrongBrush"] = CreateBrush("#7E4D17"),
            ["SelectionBrush"] = CreateBrush("#E8D7AE"),
            ["DangerBrush"] = CreateBrush("#B8412F"),
            ["ScrollTrackBrush"] = CreateBrush("#E5D5AE"),
        };
    }

    private static SolidColorBrush CreateBrush(string value)
    {
        var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value));
        brush.Freeze();
        return brush;
    }
}
