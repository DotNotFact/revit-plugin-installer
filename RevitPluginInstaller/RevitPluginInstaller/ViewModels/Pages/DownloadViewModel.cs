using Microsoft.Win32;
using RevitPluginInstaller.Infrastructure.Comands.Base;
using RevitPluginInstaller.Managers.Abstracts;
using RevitPluginInstaller.Models;
using RevitPluginInstaller.Services.Abstracts;
using RevitPluginInstaller.ViewModels.Base;
using RevitPluginInstaller.ViewModels.Core;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace RevitPluginInstaller.ViewModels.Pages;

/// <summary>"Установленные" section: plugins of the selected Revit version plus files queued for installation.</summary>
public class DownloadViewModel : ViewModel
{
    #region [ DI ]

    private readonly ILoggerManager _logger;
    private readonly ISettingsService _settingsService;
    private readonly IPluginService _pluginService;

    #endregion

    #region [ State ]

    private readonly List<Plugin> _pending = [];

    public ObservableCollection<PluginItemViewModel> Plugins { get; } = [];

    private string _version = string.Empty;
    public string Version
    {
        get => _version;
        private set
        {
            if (Set(ref _version, value))
                OnPropertyChanged(nameof(Title));
        }
    }

    public string Title => string.IsNullOrEmpty(Version) ? "Плагины" : $"Плагины - Revit {Version}";

    private string _summary = string.Empty;
    public string Summary
    {
        get => _summary;
        private set => Set(ref _summary, value);
    }

    private int _pendingCount;
    public int PendingCount
    {
        get => _pendingCount;
        private set
        {
            if (Set(ref _pendingCount, value))
            {
                OnPropertyChanged(nameof(HasPending));
                OnPropertyChanged(nameof(InstallButtonText));
            }
        }
    }

    public bool HasPending => PendingCount > 0;
    public string InstallButtonText => HasPending ? $"Установить ({PendingCount})" : "+ Установить";

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set => Set(ref _isBusy, value);
    }

    private int _installationProgress;
    public int InstallationProgress
    {
        get => _installationProgress;
        set => Set(ref _installationProgress, value);
    }

    private string _statusText = "Готово";
    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    private bool _isEmpty;
    public bool IsEmpty
    {
        get => _isEmpty;
        private set => Set(ref _isEmpty, value);
    }

    public string DropHint => "Перетащите сюда .dll / .addin файлы или папку плагина - установка с автоматическим бэкапом";

    #endregion

    #region [ Commands ]

    public ICommand RefreshCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand BrowseCommand { get; }
    public ICommand RemoveAllCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand BackupCommand { get; }
    public ICommand OpenLocationCommand { get; }
    public ICommand DropCommand { get; }

    #endregion

    public DownloadViewModel(
        IPluginService pluginService,
        ISettingsService settingsService,
        ILoggerManager logger)
    {
        _pluginService = pluginService;
        _settingsService = settingsService;
        _logger = logger;

        RefreshCommand = new ActionCommand(_ => Load());
        ExportCommand = new ActionCommand(OnExportAsync, _ => !IsBusy);
        InstallCommand = new ActionCommand(OnInstallAsync, _ => !IsBusy);
        BrowseCommand = new ActionCommand(_ => Browse(), _ => !IsBusy);
        RemoveAllCommand = new ActionCommand(OnRemoveAllAsync, _ => !IsBusy && Plugins.Any(p => p.IsInstalled));
        RemoveCommand = new ActionCommand(OnRemoveAsync, _ => !IsBusy);
        BackupCommand = new ActionCommand(OnBackupAsync, p => !IsBusy && p is PluginItemViewModel { IsInstalled: true });
        OpenLocationCommand = new ActionCommand(OnOpenLocation);
        DropCommand = new ActionCommand(OnDrop, _ => !IsBusy);

        Load();
    }

    #region [ Loading ]

    private void Load()
    {
        Version = _settingsService.GetSelectedVersionAsync().GetAwaiter().GetResult();

        var packs = _pluginService.GetPluginsForVersionAsync(Version)
            .OrderByDescending(p => p.InstallationDate)
            .ToList();

        Plugins.Clear();

        foreach (var plugin in _pending)
            Plugins.Add(new PluginItemViewModel(plugin, new PluginPack { Version = Version }));

        foreach (var pack in packs)
            foreach (var plugin in pack.Plugins)
                Plugins.Add(new PluginItemViewModel(plugin, pack));

        PendingCount = _pending.Count;
        IsEmpty = Plugins.Count == 0;
        UpdateSummary();
        CommandManager.InvalidateRequerySuggested();
    }

    private void UpdateSummary()
    {
        var installed = Plugins.Count(p => p.IsInstalled);
        var backups = _pluginService.GetBackups().Count;

        var parts = new List<string> { $"{installed} {PluralInstalled(installed)}" };
        if (backups > 0) parts.Add($"{backups} {PluralBackup(backups)}");
        if (PendingCount > 0) parts.Add($"{PendingCount} к установке");

        Summary = string.Join(" · ", parts);
    }

    #endregion

    #region [ Pending files ]

    private void OnDrop(object? p)
    {
        if (p is not IDataObject dataObject || !dataObject.GetDataPresent(DataFormats.FileDrop))
            return;

        if (dataObject.GetData(DataFormats.FileDrop) is string[] files)
            Enqueue(files);
    }

    private void Browse()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите файлы плагина",
            Multiselect = true,
            Filter = "Плагины Revit (*.addin;*.dll)|*.addin;*.dll|Все файлы (*.*)|*.*",
        };

        if (dialog.ShowDialog() == true)
            Enqueue(dialog.FileNames);
    }

    private void Enqueue(IEnumerable<string> paths)
    {
        var added = 0;

        foreach (var path in paths)
        {
            if (_pending.Any(x => string.Equals(x.Link, path, StringComparison.OrdinalIgnoreCase)))
                continue;

            _pending.Add(new Plugin
            {
                Id = Guid.NewGuid(),
                Name = Path.GetFileName(path),
                Link = path,
                IsDrop = true,
            });
            added++;
        }

        if (added > 0)
        {
            StatusText = $"В очереди на установку: {_pending.Count}";
            Load();
        }
    }

    #endregion

    #region [ Install ]

    private async void OnInstallAsync(object? _)
    {
        if (_pending.Count == 0)
        {
            Browse();
            if (_pending.Count == 0)
                return;
        }

        if (string.IsNullOrEmpty(Version))
        {
            MessageBox.Show("Сначала выберите версию Revit в боковой панели.", "Версия не выбрана", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (IsRevitRunning())
        {
            MessageBox.Show("Закройте Revit перед установкой плагинов.", "Revit запущен", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var files = _pending.Select(p => p.Link).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var count = files.Count;

        try
        {
            IsBusy = true;
            InstallationProgress = 0;
            StatusText = $"Установка {count} {PluralFiles(count)} - копирование…";

            var progress = new Progress<int>(value =>
            {
                InstallationProgress = value;
                StatusText = $"Установка в Revit {Version} - копирование файлов… {value}%";
            });

            await _pluginService.InstallPluginsAsync(files, Version, progress);

            _pending.Clear();
            StatusText = $"Установлено: {count} {PluralFiles(count)} · Revit {Version}";
        }
        catch (Exception ex)
        {
            await _logger.LogAsync($"[Error] Install failed: {ex}");
            StatusText = "Ошибка установки - подробности в логе";
            MessageBox.Show(ex.Message, "Не удалось установить", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            Load();
        }
    }

    #endregion

    #region [ Remove / backup ]

    private async void OnRemoveAsync(object? p)
    {
        if (p is not PluginItemViewModel item)
            return;

        if (item.IsPending)
        {
            _pending.RemoveAll(x => x.Link == item.Plugin.Link);
            StatusText = _pending.Count == 0 ? "Очередь установки пуста" : $"В очереди на установку: {_pending.Count}";
            Load();
            return;
        }

        if (IsRevitRunning())
        {
            MessageBox.Show("Закройте Revit перед удалением плагинов.", "Revit запущен", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Удалить «{item.Name}»?\nПеред удалением будет создана резервная копия.",
            "Удаление плагина", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            IsBusy = true;
            StatusText = $"Бэкап и удаление «{item.Name}»…";
            await _pluginService.CreateBackupAsync(item.Plugin);
            await _pluginService.RemovePluginAsync(item.Plugin);
            StatusText = $"Удалён «{item.Name}», бэкап сохранён";
        }
        catch (Exception ex)
        {
            await _logger.LogAsync($"[Error] Remove failed: {ex}");
            StatusText = "Ошибка удаления - подробности в логе";
            MessageBox.Show(ex.Message, "Не удалось удалить", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            Load();
        }
    }

    private async void OnRemoveAllAsync(object? _)
    {
        if (IsRevitRunning())
        {
            MessageBox.Show("Закройте Revit перед удалением плагинов.", "Revit запущен", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            "Удалить все установленные плагины?\nДля каждого будет создана резервная копия.",
            "Удаление всех плагинов", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            IsBusy = true;
            StatusText = "Создание бэкапов и удаление…";
            await _pluginService.RemoveAllPluginsAsync();
            StatusText = "Все плагины удалены, бэкапы сохранены";
        }
        catch (Exception ex)
        {
            await _logger.LogAsync($"[Error] Remove all failed: {ex}");
            StatusText = "Ошибка удаления - подробности в логе";
            MessageBox.Show(ex.Message, "Не удалось удалить", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            Load();
        }
    }

    private async void OnBackupAsync(object? p)
    {
        if (p is not PluginItemViewModel item)
            return;

        try
        {
            IsBusy = true;
            StatusText = $"Создание бэкапа «{item.Name}»…";
            await _pluginService.CreateBackupAsync(item.Plugin);
            StatusText = $"Бэкап «{item.Name}» создан";
        }
        catch (Exception ex)
        {
            await _logger.LogAsync($"[Error] Backup failed: {ex}");
            StatusText = "Ошибка бэкапа - подробности в логе";
        }
        finally
        {
            IsBusy = false;
            UpdateSummary();
        }
    }

    private void OnOpenLocation(object? p)
    {
        if (p is not PluginItemViewModel item)
            return;

        var path = item.Plugin.Link;

        if (File.Exists(path))
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        else if (Directory.Exists(path))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        else
            StatusText = "Файл не найден на диске";
    }

    #endregion

    #region [ Export ]

    private async void OnExportAsync(object? _)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Экспорт списка плагинов",
            Filter = "JSON (*.json)|*.json",
            DefaultExt = "json",
            FileName = $"RevitPlugins-{DateTime.Now:yyyyMMdd}",
        };

        if (dialog.ShowDialog() != true)
            return;

        await _pluginService.ExportPluginsAsync(dialog.FileName);
        StatusText = $"Экспортировано в {Path.GetFileName(dialog.FileName)}";
    }

    #endregion

    #region [ Helpers ]

    private static bool IsRevitRunning() => Process.GetProcessesByName("Revit").Length > 0;

    private static string PluralInstalled(int n) => "установлено";

    private static string PluralBackup(int n)
    {
        var m10 = n % 10; var m100 = n % 100;
        if (m10 == 1 && m100 != 11) return "бэкап";
        if (m10 is >= 2 and <= 4 && m100 is < 12 or > 14) return "бэкапа";
        return "бэкапов";
    }

    private static string PluralFiles(int n)
    {
        var m10 = n % 10; var m100 = n % 100;
        if (m10 == 1 && m100 != 11) return "файл";
        if (m10 is >= 2 and <= 4 && m100 is < 12 or > 14) return "файла";
        return "файлов";
    }

    #endregion
}
