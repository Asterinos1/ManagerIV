using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ManagerIV.Core;

public class PlaylistIdToDeleteButtonVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string id && id == "all_songs" ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
