using RevitPluginInstaller.Infrastructure.Comands.Base;
using RevitPluginInstaller.Services.Abstracts;
using RevitPluginInstaller.Managers.Abstracts;
using RevitPluginInstaller.ViewModels.Base;
using RevitPluginInstaller.ViewModels.Core;
using System.Collections.ObjectModel;
using RevitPluginInstaller.Models;
using System.Windows.Input;
using System.Diagnostics;
using Microsoft.Win32;
using System.Windows;
using System.IO;
using System.Collections;
using System;

namespace RevitPluginInstaller.ViewModels.Pages;

public class DownloadViewModel : ViewModel
{
    #region [ Managers ]

    private readonly ILoggerManager _logger;

    #endregion

    #region [ Services ]

    private readonly ISettingsService _settingsService;
    private readonly IPluginService _pluginService;
    private readonly IFileService _fileService;

    #endregion

    #region [ Variables ]

    #region [ PluginPacks ]

    public ObservableCollection<TreeViewItemViewModel> PluginPacks { get; } = [];

    #endregion

    #region [ PluginResponses ]

    private PluginResponse _pluginResponses = new();
    public PluginResponse PluginResponses
    {
        get => _pluginResponses;
        set => Set(ref _pluginResponses, value);
    }

    #endregion

    #region [ InstallationProgress ]

    private int _installationProgress;
    public int InstallationProgress
    {
        get => _installationProgress;
        set => Set(ref _installationProgress, value);
    }

    #endregion

    #region [ Heading ]

    public string Heading => "Перетащите сюда все необходимые плагины";

    #endregion

    #endregion

    #region [ Commands ]

    #region [ DropCommand ]

    public ICommand DropCommand { get; private set; } = null!;

    private async void DropCommandExecuteAsync(object? p)
    {
        if (p is not IDataObject dataObject || !dataObject.GetDataPresent(DataFormats.FileDrop))
            return;

        string[] files = (string[])dataObject.GetData(DataFormats.FileDrop);
        var selectedVersion = await _settingsService.GetSelectedVersionAsync();

        var pluginPack = new PluginPack
        {
            Id = Guid.NewGuid(),
            Name = files.Length.ToString(),
            Version = selectedVersion,
            InstallationDate = DateTime.Now,
            Plugins = files.Select(file => new Plugin
            {
                Id = Guid.NewGuid(),
                Name = Path.GetFileName(file),
                Link = file,
                IsDrop = true
            }).ToList()
        };

        PluginResponses.PluginPacks.Add(pluginPack);
        UpdateTreeView();
    }

    #endregion

    #region [ InstallPluginCommand ]

    public ICommand InstallPluginCommand { get; private set; } = null!;

    private async void OnInstallPluginCommandExecuteAsync(object? p)
    {
        var selectedVersion = await _settingsService.GetSelectedVersionAsync();

        var pluginsToInstall = _pluginResponses.PluginPacks
            .SelectMany(pack => pack.Plugins)
            .Where(plugin => plugin.IsDrop)
            .GroupBy(plugin => plugin.Link)
            .Select(group => group.First())
            .ToList();

        var count = pluginsToInstall.Count;

        if (count == 0)
        {
            MessageBox.Show("Пожалуйста, выберите плагин");
            return;
        }

        if (!await CheckRevitRunningAsync())
        {
            MessageBox.Show("Пожалуйста закройте Revit перед установкой плагина.", "Работает Revit");
            return;
        }

        InstallationProgress = 0;
        var progress = new Progress<int>(value => InstallationProgress = value);

        await _pluginService.InstallPluginsAsync(pluginsToInstall.Select(p => p.Link), selectedVersion, progress);
        LoadPlugins();

        await ShowMessageAsync($"Установлено плагинов: {count}");
    }

    #endregion

    #region [ RemoveAllPluginsCommand ]

    public ICommand RemoveAllPluginsCommand { get; private set; } = null!;

    private async void OnRemoveAllPluginsCommandExecuteAsync(object? p)
    {
        if (!PluginResponses.PluginPacks.Any())
        {
            MessageBox.Show("Нет установленных плагинов.");
            return;
        }

        if (!await CheckRevitRunningAsync())
        {
            MessageBox.Show("Пожалуйста закройте Revit перед удалением плагинов.", "Работает Revit");
            return;
        }

        var result = await ShowConfirmationAsync("Вы уверены, что хотите удалить все плагины? Будут созданы резервные копии.");

        if (result == MessageBoxResult.Yes)
        {
            await _pluginService.RemoveAllPluginsAsync();
            LoadPlugins();

            await _logger.LogAsync("Removed all plugins");
            await ShowMessageAsync("Все плагины удалены, резервные копии сохранены.");
        }
    }

    #endregion

    #region [ RemovePluginCommand ]

    public ICommand RemovePluginCommand { get; private set; } = null!;

    private async void OnRemovePluginCommandExecuteAsync(object? p)
    {
        if (p is not Plugin plugin)
            return;

        if (!await CheckRevitRunningAsync())
        {
            MessageBox.Show("Пожалуйста закройте Revit перед установкой плагина.", "Работает Revit");
            return;
        }

        var result = await ShowConfirmationAsync("Вы уверены, что хотите удалить этот плагин? Будет создана резервная копия.");

        if (result == MessageBoxResult.Yes)
        {
            await RemovePluginAsync(plugin);
            UpdateTreeView();

            LoadPlugins();
        }
    }

    private async Task RemovePluginAsync(Plugin plugin)
    {
        if (plugin.IsDrop)
        {
            PluginResponses.PluginPacks.RemoveAll(pack => pack.Plugins.Contains(plugin));
        }
        else
        {
            await _pluginService.CreateBackupAsync(plugin);
            await _pluginService.RemovePluginAsync(plugin);
            PluginResponses.PluginPacks.RemoveAll(pp => pp.Plugins.Any(p => p.Link == plugin.Link));
        }

        await _logger.LogAsync($"Removed plugin: {plugin.Name}");
    }

    #endregion 

    #region [ CheckRevitVersionsCommand ]

    public ICommand CheckRevitVersionsCommand { get; private set; } = null!;

    private async void OnCheckRevitVersionsCommandExecuteAsync(object? p)
    {
        var revitPath = await _settingsService.GetRevitPathAsync();
        var versions = _pluginService.GetAvailableRevitVersions(revitPath);

        var message = string.Join(Environment.NewLine, versions);
        await ShowMessageAsync($"Доступные Revit версии:{Environment.NewLine}{message}");
    }

    #endregion

    #region [ ImportPluginsCommand ]

    public ICommand ImportPluginsCommand { get; private set; } = null!;

    private async void OnImportPluginsCommandExecuteAsync(object? p)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            DefaultExt = "json"
        };

        if (dialog.ShowDialog() == true)
        {
            await _pluginService.ImportPluginsAsync(dialog.FileName);
            LoadPlugins();
            await _logger.LogAsync($"Imported plugins from: {dialog.FileName}");
        }
    }

    #endregion

    #region [ ExportPluginsCommand ]

    public ICommand ExportPluginsCommand { get; private set; } = null!;

    private async void OnExportPluginsCommandExecuteAsync(object? p)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            DefaultExt = "json",
            FileName = "RevitPlugins"
        };

        if (dialog.ShowDialog() == true)
        {
            await _pluginService.ExportPluginsAsync(dialog.FileName);
            await _logger.LogAsync($"Exported plugins to: {dialog.FileName}");
        }
    }

    #endregion

    #endregion

    public DownloadViewModel(
        IPluginService pluginService,
        ISettingsService settingsService,
        IFileService fileService,
        ILoggerManager logger)
    {
        _settingsService = settingsService;
        _pluginService = pluginService;
        _fileService = fileService;
        _logger = logger;

        _logger.LogAsync("[Information] - Initializing DownloadViewModel");

        PluginResponses = new();

        InitializeCommands();
        LoadPlugins();

        _logger.LogAsync("DownloadViewModel initialized");
    }

    private void InitializeCommands()
    {
        CheckRevitVersionsCommand = new ActionCommand(OnCheckRevitVersionsCommandExecuteAsync);
        ImportPluginsCommand = new ActionCommand(OnImportPluginsCommandExecuteAsync);
        ExportPluginsCommand = new ActionCommand(OnExportPluginsCommandExecuteAsync);
        RemovePluginCommand = new ActionCommand(OnRemovePluginCommandExecuteAsync);
        RemoveAllPluginsCommand = new ActionCommand(OnRemoveAllPluginsCommandExecuteAsync);
        InstallPluginCommand = new ActionCommand(OnInstallPluginCommandExecuteAsync);
        DropCommand = new ActionCommand(DropCommandExecuteAsync);
    }

    private async void LoadPlugins()
    {
        InstallationProgress = 0;

        var selectedVersion = await _settingsService.GetSelectedVersionAsync();
        var pluginResponse = _pluginService.GetPluginsForVersionAsync(selectedVersion);

        PluginResponses.PluginPacks = pluginResponse.ToList();
        UpdateTreeView();
    }

    private void UpdateTreeView()
    {
        PluginPacks.Clear();
        foreach (var pack in PluginResponses.PluginPacks)
        {
            var packItem = CreatePackTreeViewItem(pack);
            PluginPacks.Add(packItem);
        }
    }

    private TreeViewItemViewModel CreatePackTreeViewItem(PluginPack pack)
    {
        var packItem = new TreeViewItemViewModel
        {
            DisplayName = $"Pack: {pack.Plugins.Count} - {pack.Version} - Installed: {pack.InstallationDate:s}",
            IsExpanded = false,
            ItemType = TreeViewItemType.Pack,
            Data = pack
        };

        foreach (var plugin in pack.Plugins)
            packItem.Children.Add(CreatePluginTreeViewItem(plugin));

        return packItem;
    }

    private TreeViewItemViewModel CreatePluginTreeViewItem(Plugin plugin)
    {
        var pluginItem = new TreeViewItemViewModel
        {
            DisplayName = plugin.Name,
            IsExpanded = false,
            ItemType = TreeViewItemType.Plugin,
            Data = plugin,
            RemovePluginCommand = RemovePluginCommand
        };

        pluginItem.Children.Add(new TreeViewItemViewModel
        {
            DisplayName = plugin.Link,
            IsExpanded = false,
            ItemType = TreeViewItemType.Path,
            Data = plugin.Link
        });

        return pluginItem;
    }

    private Task<bool> CheckRevitRunningAsync() =>
       Task.Run(() => Process.GetProcessesByName("Revit").Length == 0);

    private Task ShowMessageAsync(string message) =>
        Task.Run(() => MessageBox.Show(message));

    private Task<MessageBoxResult> ShowConfirmationAsync(string message) =>
        Task.Run(() => MessageBox.Show(message, "Confirm", MessageBoxButton.YesNo));

}