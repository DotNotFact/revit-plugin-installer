using Material.Icons;
using RevitPluginInstaller.Models;
using RevitPluginInstaller.Utils;
using RevitPluginInstaller.ViewModels.Base;
using System.IO;

namespace RevitPluginInstaller.ViewModels.Core;

public enum PluginStatus
{
    Installed,
    Pending,
    Missing,
}

public class PluginItemViewModel : ViewModel
{
    public Plugin Plugin { get; }
    public PluginPack Pack { get; }

    public string Name { get; }
    public string FileName { get; }
    public string SizeText { get; }
    public string DateText { get; }
    public PluginStatus Status { get; }
    public MaterialIconKind Icon { get; }

    public bool IsPending => Status == PluginStatus.Pending;
    public bool IsInstalled => Status == PluginStatus.Installed;

    public string StatusText => Status switch
    {
        PluginStatus.Pending => "К установке",
        PluginStatus.Missing => "Файл не найден",
        _ => "Активен",
    };

    /// <summary>Meta line: "WallFramer.addin · 4,2 МБ · установлен 12.08.2026".</summary>
    public string Meta { get; }

    public PluginItemViewModel(Plugin plugin, PluginPack pack)
    {
        Plugin = plugin;
        Pack = pack;

        FileName = string.IsNullOrEmpty(plugin.Name) ? Path.GetFileName(plugin.Link) : plugin.Name;
        Name = ToDisplayName(FileName);

        var exists = File.Exists(plugin.Link) || Directory.Exists(plugin.Link);
        Status = plugin.IsDrop ? PluginStatus.Pending : exists ? PluginStatus.Installed : PluginStatus.Missing;

        var size = FileSizeFormatter.SizeOf(plugin.Link);
        SizeText = FileSizeFormatter.Format(size);
        DateText = pack.InstallationDate == default ? string.Empty : pack.InstallationDate.ToString("dd.MM.yyyy");

        Icon = PickIcon(plugin.Link, FileName);

        var parts = new List<string> { FileName };
        if (size > 0) parts.Add(SizeText);
        parts.Add(Status switch
        {
            PluginStatus.Pending => "будет установлен из папки " + (Path.GetFileName(Path.GetDirectoryName(plugin.Link)) ?? "?"),
            PluginStatus.Missing => "файл отсутствует на диске",
            _ => string.IsNullOrEmpty(DateText) ? "установлен" : "установлен " + DateText,
        });
        Meta = string.Join(" · ", parts);
    }

    private static string ToDisplayName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(name))
            name = fileName;

        name = name.Replace('_', ' ').Replace('-', ' ');

        // Split CamelCase: "WallFramerPro" -> "Wall Framer Pro"
        var chars = new List<char>(name.Length + 8);
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && char.IsUpper(c) && char.IsLower(name[i - 1]))
                chars.Add(' ');
            chars.Add(c);
        }

        return new string(chars.ToArray()).Trim();
    }

    private static MaterialIconKind PickIcon(string link, string fileName)
    {
        if (Directory.Exists(link))
            return MaterialIconKind.FolderOutline;

        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".addin" => MaterialIconKind.PuzzleOutline,
            ".dll" => MaterialIconKind.CodeBraces,
            ".exe" => MaterialIconKind.Application,
            ".zip" or ".7z" or ".rar" => MaterialIconKind.FolderZipOutline,
            ".json" or ".xml" or ".config" => MaterialIconKind.CodeJson,
            ".dyn" => MaterialIconKind.Graph,
            ".rfa" or ".rvt" or ".rte" => MaterialIconKind.CubeOutline,
            _ => MaterialIconKind.FileOutline,
        };
    }
}
