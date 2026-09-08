using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RevitPluginInstaller.Converters;

/// <summary>Visible when the bound count (or collection) is non-empty; inverted shows for empty.</summary>
public class CountToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var count = value switch
        {
            int i => i,
            ICollection c => c.Count,
            IEnumerable e => e.Cast<object>().Count(),
            _ => 0,
        };

        var visible = count > 0;
        if (Invert) visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
