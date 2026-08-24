using RevitPluginInstaller.Managers.Abstracts;
using RevitPluginInstaller.Services.Abstracts;
using RevitPluginInstaller.Models;
using Newtonsoft.Json;
using System.IO;

namespace RevitPluginInstaller.Services.Bases;

public class PluginService : IPluginService
{
    #region [ Fields ]

    private const string PluginsFileName = "plugins.json";
    private const string BackupFolderName = "backups";

    private PluginResponse _pluginResponses = new();

    #endregion

    #region [ DI ]

    private readonly ISettingsService _settingsService;
    private readonly IFileService _fileService;
    private readonly ILoggerManager _logger;

    #endregion

    public PluginService(ISettingsService settingsService, IFileService fileService, ILoggerManager logger)
    {
        _settingsService = settingsService;
        _fileService = fileService;
        _logger = logger;

        LoadPlugins();
    }

    #region [ Получение плагинов (версий Revit) ]

    public IEnumerable<string> GetAvailableRevitVersions(string revitPath)
    {
        if (string.IsNullOrEmpty(revitPath))
            return [];

        var addinsPath = Path.Combine(revitPath, "Addins");

        if (!Directory.Exists(addinsPath))
            return [];

        return Directory.GetDirectories(addinsPath).Select(Path.GetFileName)!;
    }

    public IEnumerable<PluginPack> GetPluginsForVersionAsync(string version)
    {
        return _pluginResponses.PluginPacks.Where(p => p.Version == version);
    }

    public IEnumerable<PluginPack> GetAllPluginsAsync()
    {
        return _pluginResponses.PluginPacks;
    }

    #endregion

    #region [ Установка плагинов ]

    public async Task InstallPluginsAsync(IEnumerable<string> files, string version, IProgress<int>? progress = null)
    {
        var revitPath = await _settingsService.GetRevitPathAsync();
        var destinationPath = Path.Combine(revitPath, "Addins", version);

        var newPluginPack = new PluginPack
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(files.First()),
            Version = version,
            InstallationDate = DateTime.Now,
        };

        var fileList = files.ToList();

        for (int i = 0; i < fileList.Count; i++)
        {
            var sourcePath = fileList[i];
            string fileName = Path.GetFileName(sourcePath);
            string destinationFilePath = Path.Combine(destinationPath, fileName);

            // Check if the plugin already exists
            var existingPlugin = _pluginResponses.PluginPacks
                .Where(p => p.Version == version)
                .SelectMany(p => p.Plugins)
                .FirstOrDefault(p => p.Link == destinationFilePath);

            if (existingPlugin is not null)
            {
                // Create backup of the existing plugin
                await CreateBackupAsync(existingPlugin);

                // Удаление существующего плагина из соответствующего PluginPack
                foreach (var pack in _pluginResponses.PluginPacks.ToList())
                {
                    pack.Plugins.RemoveAll(p => p.Link == existingPlugin.Link);

                    if (pack.Plugins.Count == 0)
                        _pluginResponses.PluginPacks.Remove(pack);
                }

                // Delete the existing file or directory
                if (IsFile(destinationFilePath))
                {
                    await _fileService.DeleteFileAsync(destinationFilePath);
                }
                else if (IsDirectory(destinationFilePath))
                {
                    Directory.Delete(destinationFilePath, true);
                }
            }

            if (IsFile(sourcePath))
            {
                await _fileService.CopyFileAsync(sourcePath, destinationFilePath);
            }
            else if (IsDirectory(sourcePath))
            {
                await _fileService.CopyDirectoryAsync(sourcePath, Path.Combine(destinationPath, Path.GetFileName(sourcePath)));
            }

            var newPlugin = new Plugin()
            {
                Id = Guid.NewGuid(),
                Name = fileName,
                Link = destinationFilePath,
                IsDrop = false
            };
            newPluginPack.Plugins.Add(newPlugin);

            progress?.Report((int)((i + 1.0) / fileList.Count * 100));
        }

        _pluginResponses.PluginPacks.Add(newPluginPack);

        await SavePluginsAsync();
        await _logger.LogAsync($"Installed plugin: {newPluginPack.Name}");
    }

    #endregion

    #region [ Удаление плагинов ]

    public async Task RemovePluginAsync(Plugin plugin)
    {
        var packWithPlugin = _pluginResponses.PluginPacks.FirstOrDefault(pack => pack.Plugins.Contains(plugin));

        if (packWithPlugin is not null)
        {
            packWithPlugin.Plugins.Remove(plugin);
            await _logger.LogAsync($"Plugin {plugin.Name} successfully removed.");

            if (packWithPlugin.Plugins.Count == 0)
            {
                _pluginResponses.PluginPacks.Remove(packWithPlugin);
                await _logger.LogAsync($"Empty pack {packWithPlugin.Name} removed.");
            }
        }
        else
        {
            await _logger.LogAsync($"Plugin {plugin.Name} not found for removal.");
        }

        await RemoveFileFromPluginAsync(plugin.Link);
        await _logger.LogAsync($"Removed plugin: {plugin.Name}");
    }

    public async Task RemoveFileFromPluginAsync(string filePath)
    {
        if (IsFile(filePath))
        {
            await _fileService.DeleteFileAsync(filePath);
        }
        else if (Directory.Exists(filePath))
        {
            Directory.Delete(filePath, true);
        }

        await SavePluginsAsync();
        await _logger.LogAsync($"Removed file: {filePath}");
    }

    public async Task RemoveAllPluginsAsync()
    {
        var allPlugins = _pluginResponses.PluginPacks
            .SelectMany(pack => pack.Plugins)
            .ToList();

        foreach (var plugin in allPlugins)
        {
            await CreateBackupAsync(plugin);

            if (IsFile(plugin.Link))
            {
                await _fileService.DeleteFileAsync(plugin.Link);
            }
            else if (IsDirectory(plugin.Link))
            {
                Directory.Delete(plugin.Link, true);
            }
        }

        _pluginResponses.PluginPacks.Clear();

        await SavePluginsAsync();
        await _logger.LogAsync($"Removed all plugins ({allPlugins.Count}), backups created.");
    }

    #endregion

    #region [ Export/Import & Backup ]

    public async Task ExportPluginsAsync(string filePath)
    {
        var json = JsonConvert.SerializeObject(_pluginResponses, Formatting.Indented);

        await File.WriteAllTextAsync(filePath, json);
        await _logger.LogAsync($"Exported plugins to: {filePath}");
    }

    public async Task ImportPluginsAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var importedPlugins = JsonConvert.DeserializeObject<PluginResponse>(json);

        if (importedPlugins != null)
        {
            _pluginResponses = importedPlugins;

            await SavePluginsAsync();
            await _logger.LogAsync($"Imported plugins from: {filePath}");
        }
    }

    public async Task CreateBackupAsync(Plugin plugin)
    {
        var revitPath = await _settingsService.GetRevitPathAsync();
        var backupPath = Path.Combine(revitPath, BackupFolderName, DateTime.Now.ToString("yyyyMMddHHmmss"));

        Directory.CreateDirectory(backupPath);

        var destinationPath = Path.Combine(backupPath, Path.GetFileName(plugin.Link));

        if (IsFile(plugin.Link))
        {
            await _fileService.CopyFileAsync(plugin.Link, destinationPath);
        }
        else if (IsDirectory(plugin.Link))
        {
            await _fileService.CopyDirectoryAsync(plugin.Link, destinationPath);
        }

        await _logger.LogAsync($"Created backup for plugin: {plugin.Name}");
    }

    #endregion

    private async Task SavePluginsAsync()
    {
        var pluginsPath = Path.Combine(await _settingsService.GetRevitPathAsync(), PluginsFileName);
        var json = JsonConvert.SerializeObject(_pluginResponses, Formatting.Indented);

        await File.WriteAllTextAsync(pluginsPath, json);
    }

    private void LoadPlugins()
    {
        var revitPath = _settingsService.GetRevitPathAsync().GetAwaiter().GetResult();

        if (string.IsNullOrEmpty(revitPath))
        {
            _pluginResponses = new();
            return;
        }

        var pluginsPath = Path.Combine(revitPath, PluginsFileName);

        if (File.Exists(pluginsPath))
        {
            var json = File.ReadAllText(pluginsPath);
            _pluginResponses = JsonConvert.DeserializeObject<PluginResponse>(json) ?? new();
        }
        else
        {
            _pluginResponses = new();
        }
    }

    static bool IsDirectory(string path)
    {
        if (!Directory.Exists(path))
            return false;

        FileAttributes attr = File.GetAttributes(path);
        return (attr & FileAttributes.Directory) == FileAttributes.Directory;
    }

    static bool IsFile(string path)
    {
        return File.Exists(path);
    }
}
