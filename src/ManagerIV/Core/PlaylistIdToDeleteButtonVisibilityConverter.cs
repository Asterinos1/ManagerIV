using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ManagerIV.Core;

/// <summary>
/// Converts a playlist ID to Visibility for a delete button. The default 'all_songs' playlist cannot be deleted, so it collapses.
/// </summary>
public class PlaylistIdToDeleteButtonVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Converts a playlist ID to a Visibility value.
    /// </summary>
    /// <param name="value">The playlist ID string.</param>
    /// <param name="targetType">The type of the binding target property.</param>
    /// <param name="parameter">The converter parameter to use.</param>
    /// <param name="culture">The culture to use in the converter.</param>
    /// <returns>Visibility.Collapsed if the ID is "all_songs"; otherwise, Visibility.Visible.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string id && id == "all_songs" ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Not implemented.
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
