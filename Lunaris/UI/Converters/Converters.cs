using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Lunaris.UI.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (Invert)
            flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

public sealed class StringEqualsToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? parameter?.ToString() ?? string.Empty : Binding.DoNothing;
}

public sealed class DoubleEqualsToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is double d && double.TryParse(parameter?.ToString(), out var p) && Math.Abs(d - p) < 0.001;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true && double.TryParse(parameter?.ToString(), out var p) ? p : Binding.DoNothing;
}