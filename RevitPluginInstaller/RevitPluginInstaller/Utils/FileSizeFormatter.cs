using System.Globalization;
using System.IO;

namespace RevitPluginInstaller.Utils;

public static class FileSizeFormatter
{
    private static readonly string[] Units = ["Б", "КБ", "МБ", "ГБ"];

    public static string Format(long bytes)
    {
        if (bytes <= 0)
            return "—";

        double value = bytes;
        int unit = 0;

        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var text = unit == 0
            ? value.ToString("0", CultureInfo.GetCultureInfo("ru-RU"))
            : value.ToString(value < 10 ? "0.0" : "0", CultureInfo.GetCultureInfo("ru-RU"));

        return $"{text} {Units[unit]}";
    }

    /// <summary>Size of a file, or the recursive size of a directory. Zero when the path is missing.</summary>
    public static long SizeOf(string path)
    {
        try
        {
            if (File.Exists(path))
                return new FileInfo(path).Length;

            if (Directory.Exists(path))
                return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return 0;
    }
}
