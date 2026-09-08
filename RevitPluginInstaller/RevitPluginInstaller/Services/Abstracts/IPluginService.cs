using RevitPluginInstaller.Models;

namespace RevitPluginInstaller.Services.Abstracts;

public interface IPluginService
{
    /// <summary>Raised after the plugin registry changes (install, remove, import, reload).</summary>
    event EventHandler? PluginsChanged;

    IEnumerable<string> GetAvailableRevitVersions(string revitPath);
    IEnumerable<PluginPack> GetPluginsForVersionAsync(string version);
    IEnumerable<PluginPack> GetAllPluginsAsync();

    /// <summary>Re-reads the registry from disk (e.g. after the Revit folder changed).</summary>
    void Reload();

    Task InstallPluginsAsync(IEnumerable<string> files, string version, IProgress<int>? progress = null);

    Task RemoveFileFromPluginAsync(string filePath);
    Task RemovePluginAsync(Plugin plugin);
    Task RemoveAllPluginsAsync();

    Task CreateBackupAsync(Plugin plugin);
    IReadOnlyList<BackupEntry> GetBackups();
    Task DeleteBackupAsync(BackupEntry backup);

    Task ExportPluginsAsync(string filePath);
    Task ImportPluginsAsync(string filePath);
}
