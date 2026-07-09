using System.Globalization;
using System.Windows.Data;
using AgvDispatcher.DebugDashboard.ViewModels;

namespace AgvDispatcher.DebugDashboard.Converters;

public sealed class MockDisplayNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        MockDisplayNames.ToDisplayName(value?.ToString());

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
