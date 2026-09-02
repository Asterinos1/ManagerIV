using System.Globalization;
using System.Windows.Data;

namespace ManagerIV.Core;

/// <summary>
/// Converter to return true if the bound double value is less than the parameter value.
/// Used for responsive layout styling in WPF.
/// </summary>
public class LessThanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double val && parameter is string paramStr && double.TryParse(paramStr, out double threshold))
        {
            return val < threshold;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
