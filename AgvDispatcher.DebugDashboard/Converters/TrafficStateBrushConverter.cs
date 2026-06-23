using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AgvDispatcher.DebugDashboard.Converters;

public sealed class TrafficStateBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "Locked" => new SolidColorBrush(Color.FromRgb(0x00, 0xBF, 0xFF)),
            "Occupied" => new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00)),
            "Blocked" => new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x45)),
            "Disabled" => new SolidColorBrush(Color.FromRgb(0x8B, 0x00, 0x00)),
            "Unknown" => new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
            _ => new SolidColorBrush(Color.FromRgb(0xB8, 0xC7, 0xD9))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
