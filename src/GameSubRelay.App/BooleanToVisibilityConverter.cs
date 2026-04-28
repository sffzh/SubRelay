using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GameSubRelay.App;

public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isVisible = value is bool flag && flag;

        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
        {
            isVisible = !isVisible;
        }
        else if (Invert)
        {
            isVisible = !isVisible;
        }

        if (string.Equals(parameter as string, "Thickness", StringComparison.OrdinalIgnoreCase))
        {
            return isVisible ? new Thickness(1) : new Thickness(0);
        }

        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isVisible = value switch
        {
            bool boolValue => boolValue,
            Visibility visibility => visibility == Visibility.Visible,
            _ => false
        };

        if (string.Equals(parameter as string, "Thickness", StringComparison.OrdinalIgnoreCase))
        {
            return isVisible;
        }

        return Invert ? !isVisible : isVisible;
    }
}
