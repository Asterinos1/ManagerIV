using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace ManagerIV.Core;

/// <summary>
/// Converter to calculate the 1-based index of a MusicTrack in AllTracks dynamically.
/// </summary>
public class MusicTrackToIndexConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is MusicTrack track && values[1] is IEnumerable<MusicTrack> collection)
        {
            int index = 0;
            foreach (var item in collection)
            {
                if (item.Id == track.Id)
                {
                    return (index + 1).ToString("D3");
                }
                index++;
            }
        }
        return "000";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
