using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DriverGuard.UI.Converters;

/// <summary>
/// Visível quando o valor não é nulo; recolhido quando é nulo.
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value != null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
