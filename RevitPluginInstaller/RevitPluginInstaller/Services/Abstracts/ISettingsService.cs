namespace RevitPluginInstaller.Services.Abstracts;

public interface ISettingsService
{
    /// <summary>Raised after any setting is persisted.</summary>
    event EventHandler? SettingsChanged;

    Task<string> GetRevitPathAsync();
    Task SetRevitPathAsync(string path);
    Task<string> GetSelectedVersionAsync();
    Task SetSelectedVersionAsync(string version);
}
