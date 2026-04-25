using FastTools.App.Services;
using System.Globalization;
using System.Windows.Data;

namespace FastTools.App.Converters;

public sealed class GroupDisplayNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var group = value?.ToString();
        if (string.IsNullOrWhiteSpace(group))
        {
            return string.Empty;
        }

        var localizer = System.Windows.Application.Current?.TryFindResource("Loc") as LocalizationService;
        return localizer?.Get($"Group.{group}") ?? group;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
