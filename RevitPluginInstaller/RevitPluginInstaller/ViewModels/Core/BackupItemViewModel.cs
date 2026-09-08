using RevitPluginInstaller.Models;
using RevitPluginInstaller.Utils;
using RevitPluginInstaller.ViewModels.Base;

namespace RevitPluginInstaller.ViewModels.Core;

public class BackupItemViewModel : ViewModel
{
    public BackupEntry Entry { get; }

    public string Title { get; }
    public string ItemsText { get; }
    public string SizeText { get; }
    public string FolderName { get; }
    public int Count { get; }
    public string CountText { get; }

    public BackupItemViewModel(BackupEntry entry)
    {
        Entry = entry;
        Count = entry.Items.Count;
        CountText = $"{Count} {PluralFiles(Count)}";
        Title = entry.CreatedAt.ToString("dd.MM.yyyy · HH:mm");
        FolderName = System.IO.Path.GetFileName(entry.Path);
        SizeText = FileSizeFormatter.Format(entry.SizeBytes);
        ItemsText = Count == 0
            ? "Пустая папка"
            : string.Join(", ", entry.Items.Take(4)) + (Count > 4 ? $" и ещё {Count - 4}" : string.Empty);
    }

    private static string PluralFiles(int n)
    {
        var m10 = n % 10; var m100 = n % 100;
        if (m10 == 1 && m100 != 11) return "файл";
        if (m10 is >= 2 and <= 4 && m100 is < 12 or > 14) return "файла";
        return "файлов";
    }
}
