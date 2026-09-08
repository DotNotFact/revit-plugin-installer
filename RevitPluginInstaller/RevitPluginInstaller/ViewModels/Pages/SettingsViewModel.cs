using Microsoft.Win32;
using RevitPluginInstaller.Infrastructure.Comands.Base;
using RevitPluginInstaller.Managers.Bases.Theme;
using RevitPluginInstaller.Services.Abstracts;
using RevitPluginInstaller.ViewModels.Base;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace RevitPluginInstaller.ViewModels.Pages;

public class SettingsViewModel : ViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly IPluginService _pluginService;

    public string Title => "Настройки";
    public string Summary => "Папка Revit, обнаруженные версии и оформление";

    private string _revitPath = string.Empty;
    public string RevitPath
    {
        get => _revitPath;
        private set
        {
            if (Set(ref _revitPath, value))
                OnPropertyChanged(nameof(HasRevitPath));
        }
    }

    public bool HasRevitPath => !string.IsNullOrEmpty(RevitPath);

    private string _detectedVersionsText = string.Empty;
    public string DetectedVersionsText
    {
        get => _detectedVersionsText;
        private set => Set(ref _detectedVersionsText, value);
    }

    private bool _isDarkTheme = ThemeManager.CurrentTheme == Theme.Dark;
    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            if (Set(ref _isDarkTheme, value))
                ThemeManager.ApplyTheme(value ? Theme.Dark : Theme.Light);
        }
    }

    public string LogPath => string.IsNullOrEmpty(RevitPath) ? "—" : Path.Combine(RevitPath, "RevitPluginInstaller.log");
    public string RegistryPath => string.IsNullOrEmpty(RevitPath) ? "—" : Path.Combine(RevitPath, "plugins.json");
    public string AppVersion { get; }

    public ICommand SelectFolderCommand { get; }
    public ICommand OpenLogCommand { get; }
    public ICommand OpenRevitFolderCommand { get; }

    public SettingsViewModel(ISettingsService settingsService, IPluginService pluginService)
    {
        _settingsService = settingsService;
        _pluginService = pluginService;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        AppVersion = version is null ? "v1.0" : $"v{version.Major}.{version.Minor}";

        SelectFolderCommand = new ActionCommand(OnSelectFolderAsync);
        OpenLogCommand = new ActionCommand(_ => Open(LogPath), _ => File.Exists(LogPath));
        OpenRevitFolderCommand = new ActionCommand(_ => Open(RevitPath), _ => Directory.Exists(RevitPath));

        Load();
    }

    private void Load()
    {
        RevitPath = _settingsService.GetRevitPathAsync().GetAwaiter().GetResult();

        var versions = _pluginService.GetAvailableRevitVersions(RevitPath).ToList();
        DetectedVersionsText = versions.Count == 0
            ? "Папка Addins не найдена или пуста"
            : string.Join(", ", versions.Select(v => $"Revit {v}"));

        OnPropertyChanged(nameof(LogPath));
        OnPropertyChanged(nameof(RegistryPath));
        CommandManager.InvalidateRequerySuggested();
    }

    private async void OnSelectFolderAsync(object? _)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Выберите папку Revit (в ней должна быть папка Addins)",
            Multiselect = false,
        };

        if (dialog.ShowDialog() != true)
            return;

        var folder = dialog.FolderName;

        if (!Directory.Exists(Path.Combine(folder, "Addins")))
        {
            var proceed = MessageBox.Show(
                "В выбранной папке нет подпапки Addins.\nВерсии Revit определяются по Addins\\<год>. Всё равно использовать эту папку?",
                "Папка без Addins", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (proceed != MessageBoxResult.Yes)
                return;
        }

        await _settingsService.SetRevitPathAsync(folder);

        var versions = _pluginService.GetAvailableRevitVersions(folder).ToList();
        var selected = await _settingsService.GetSelectedVersionAsync();
        if (!versions.Contains(selected))
            await _settingsService.SetSelectedVersionAsync(versions.FirstOrDefault() ?? string.Empty);

        _pluginService.Reload();
        Load();
    }

    private static void Open(string path)
    {
        if (File.Exists(path) || Directory.Exists(path))
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
