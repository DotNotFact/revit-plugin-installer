using RevitPluginInstaller.Infrastructure.Comands.Base;
using RevitPluginInstaller.Services.Abstracts;
using RevitPluginInstaller.Utils;
using RevitPluginInstaller.ViewModels.Base;
using RevitPluginInstaller.ViewModels.Core;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace RevitPluginInstaller.ViewModels.Pages;

public class BackupsViewModel : ViewModel
{
    private readonly IPluginService _pluginService;
    private readonly ISettingsService _settingsService;

    public ObservableCollection<BackupItemViewModel> Backups { get; } = [];

    public string Title => "Бэкапы";

    private string _summary = string.Empty;
    public string Summary
    {
        get => _summary;
        private set => Set(ref _summary, value);
    }

    private string _rootPath = string.Empty;
    public string RootPath
    {
        get => _rootPath;
        private set => Set(ref _rootPath, value);
    }

    private bool _isEmpty;
    public bool IsEmpty
    {
        get => _isEmpty;
        private set => Set(ref _isEmpty, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand OpenRootCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand DeleteCommand { get; }

    public BackupsViewModel(IPluginService pluginService, ISettingsService settingsService)
    {
        _pluginService = pluginService;
        _settingsService = settingsService;

        RefreshCommand = new ActionCommand(_ => Load());
        OpenRootCommand = new ActionCommand(_ => OpenFolder(RootPath), _ => Directory.Exists(RootPath));
        OpenFolderCommand = new ActionCommand(p => { if (p is BackupItemViewModel b) OpenFolder(b.Entry.Path); });
        DeleteCommand = new ActionCommand(OnDeleteAsync);

        Load();
    }

    private void Load()
    {
        var revitPath = _settingsService.GetRevitPathAsync().GetAwaiter().GetResult();
        RootPath = string.IsNullOrEmpty(revitPath) ? string.Empty : Path.Combine(revitPath, "backups");

        Backups.Clear();
        foreach (var entry in _pluginService.GetBackups())
            Backups.Add(new BackupItemViewModel(entry));

        IsEmpty = Backups.Count == 0;

        var total = Backups.Sum(b => b.Entry.SizeBytes);
        Summary = IsEmpty
            ? "Резервные копии создаются автоматически при замене и удалении плагинов"
            : $"{Backups.Count} {Plural(Backups.Count)} · {FileSizeFormatter.Format(total)}";

        CommandManager.InvalidateRequerySuggested();
    }

    private static void OpenFolder(string path)
    {
        if (Directory.Exists(path))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    private async void OnDeleteAsync(object? p)
    {
        if (p is not BackupItemViewModel backup)
            return;

        var result = MessageBox.Show(
            $"Удалить бэкап от {backup.Title}?\nВосстановить его после этого будет нельзя.",
            "Удаление бэкапа", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        await _pluginService.DeleteBackupAsync(backup.Entry);
        Load();
    }

    private static string Plural(int n)
    {
        var m10 = n % 10; var m100 = n % 100;
        if (m10 == 1 && m100 != 11) return "бэкап";
        if (m10 is >= 2 and <= 4 && m100 is < 12 or > 14) return "бэкапа";
        return "бэкапов";
    }
}
