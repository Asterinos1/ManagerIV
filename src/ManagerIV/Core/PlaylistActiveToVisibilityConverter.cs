using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ManagerIV.Core;

/// <summary>
/// Converts a playlist active state to Visibility. Visible if the current playlist matches the active playlist.
/// </summary>
public class PlaylistActiveToVisibilityConverter : IMultiValueConverter
{
    /// <summary>
    /// Converts a collection of values to a Visibility value.
    /// </summary>
    /// <param name="values">The array of values: target playlist ID and active playlist ID.</param>
    /// <param name="targetType">The type of the binding target property.</param>
    /// <param name="parameter">The converter parameter to use.</param>
    /// <param name="culture">The culture to use in the converter.</param>
    /// <returns>Visibility.Visible if the playlist is active; otherwise, Visibility.Collapsed.</returns>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is string playlistId && values[1] is string activePlaylistId)
        {
            return playlistId == activePlaylistId ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    /// <summary>
    /// Not implemented.
    /// </summary>
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
