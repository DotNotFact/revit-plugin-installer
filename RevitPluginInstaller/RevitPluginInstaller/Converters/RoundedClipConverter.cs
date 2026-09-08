using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace RevitPluginInstaller.Converters;

/// <summary>Builds a rounded-rectangle clip geometry from the element's actual width and height.</summary>
public class RoundedClipConverter : IMultiValueConverter
{
    public double Radius { get; set; } = 12;

    public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not double width || values[1] is not double height)
            return null;

        if (width <= 0 || height <= 0)
            return null;

        return new RectangleGeometry(new Rect(0, 0, width, height), Radius, Radius);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
