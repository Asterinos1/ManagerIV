using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ManagerIV.Core;

public class PlaylistActiveToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is string playlistId && values[1] is string activePlaylistId)
        {
            return playlistId == activePlaylistId ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
