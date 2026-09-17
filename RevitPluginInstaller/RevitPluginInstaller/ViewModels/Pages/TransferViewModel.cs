using Microsoft.Win32;
using RevitPluginInstaller.Infrastructure.Comands.Base;
using RevitPluginInstaller.Managers.Abstracts;
using RevitPluginInstaller.Services.Abstracts;
using RevitPluginInstaller.ViewModels.Base;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace RevitPluginInstaller.ViewModels.Pages;

public class TransferViewModel : ViewModel
{
    private readonly IPluginService _pluginService;
    private readonly ILoggerManager _logger;

    public string Title => "Экспорт / импорт";
    public string Summary => "Список установленных плагинов в JSON - чтобы перенести набор на другую машину или восстановить после переустановки";

    private int _pluginCount;
    public int PluginCount
    {
        get => _pluginCount;
        private set => Set(ref _pluginCount, value);
    }

    private int _versionCount;
    public int VersionCount
    {
        get => _versionCount;
        private set => Set(ref _versionCount, value);
    }

    private string _lastAction = "Ещё ничего не экспортировалось";
    public string LastAction
    {
        get => _lastAction;
        private set => Set(ref _lastAction, value);
    }

    public ICommand ExportCommand { get; }
    public ICommand ImportCommand { get; }

    public TransferViewModel(IPluginService pluginService, ILoggerManager logger)
    {
        _pluginService = pluginService;
        _logger = logger;

        ExportCommand = new ActionCommand(OnExportAsync, _ => PluginCount > 0);
        ImportCommand = new ActionCommand(OnImportAsync);

        Refresh();
    }

    private void Refresh()
    {
        var packs = _pluginService.GetAllPluginsAsync().ToList();
        PluginCount = packs.Sum(p => p.Plugins.Count);
        VersionCount = packs.Select(p => p.Version).Distinct().Count();
        CommandManager.InvalidateRequerySuggested();
    }

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
        LastAction = $"Экспортировано {PluginCount} плагинов → {Path.GetFileName(dialog.FileName)}";
    }

    private async void OnImportAsync(object? _)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Импорт списка плагинов",
            Filter = "JSON (*.json)|*.json",
            DefaultExt = "json",
        };

        if (dialog.ShowDialog() != true)
            return;

        var confirm = MessageBox.Show(
            "Импорт заменит текущий реестр плагинов записями из файла.\nСами файлы плагинов не копируются. Продолжить?",
            "Импорт списка", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            await _pluginService.ImportPluginsAsync(dialog.FileName);
            Refresh();
            LastAction = $"Импортировано из {Path.GetFileName(dialog.FileName)}: {PluginCount} плагинов";
        }
        catch (Exception ex)
        {
            await _logger.LogAsync($"[Error] Import failed: {ex}");
            MessageBox.Show(ex.Message, "Не удалось импортировать", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
