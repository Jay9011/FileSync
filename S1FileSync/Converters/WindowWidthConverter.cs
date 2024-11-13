using System.Globalization;
using System.Windows.Data;

namespace S1FileSync.Converters;

public class WindowWidthConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double width && parameter is string threshold)
        {
            return width < double.Parse(threshold);
        }

        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}