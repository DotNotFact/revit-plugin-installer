using System.Globalization;
using System.Windows.Data;

namespace RevitPluginInstaller.Converters;

/// <summary>Returns true when the bound value equals the converter parameter (compared as strings).</summary>
public class EqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter is not null && targetType.IsEnum)
            return Enum.Parse(targetType, parameter.ToString()!);

        return Binding.DoNothing;
    }
}
