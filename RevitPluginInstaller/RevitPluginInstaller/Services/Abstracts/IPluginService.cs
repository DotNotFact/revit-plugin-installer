using RevitPluginInstaller.Models;

namespace RevitPluginInstaller.Services.Abstracts;

public interface IPluginService
{
    IEnumerable<string> GetAvailableRevitVersions(string revitPath);
    IEnumerable<PluginPack> GetPluginsForVersionAsync(string version);
    IEnumerable<PluginPack> GetAllPluginsAsync();

    Task InstallPluginsAsync(IEnumerable<string> files, string version, IProgress<int>? progress = null);

    Task RemoveFileFromPluginAsync(string filePath);
    Task RemovePluginAsync(Plugin plugin);
    Task RemoveAllPluginsAsync();

    Task CreateBackupAsync(Plugin plugin);
    Task ExportPluginsAsync(string filePath);
    Task ImportPluginsAsync(string filePath);
}