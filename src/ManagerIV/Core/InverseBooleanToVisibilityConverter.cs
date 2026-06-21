using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ManagerIV.Core;

/// <summary>
/// Converts a boolean value to Visibility (True -> Collapsed, False -> Visible).
/// </summary>
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolVal)
        {
            return boolVal ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility != Visibility.Visible;
        }
        return false;
    }
}
