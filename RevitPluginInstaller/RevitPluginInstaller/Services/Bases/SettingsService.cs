using Newtonsoft.Json;
using RevitPluginInstaller.Models;
using RevitPluginInstaller.Services.Abstracts;
using System.IO;

namespace RevitPluginInstaller.Services.Bases;

public class SettingsService : ISettingsService
{
    private const string SettingsFileName = "settings.json";
    private readonly string _settingsFileName;
    private SettingsResponse _settings;

    public event EventHandler? SettingsChanged;

    public SettingsService()
    {
        _settingsFileName = Path.Combine(AppContext.BaseDirectory, SettingsFileName);
        _settings = LoadSettings();
    }

    private SettingsResponse LoadSettings()
    {
        if (File.Exists(_settingsFileName))
        {
            var json = File.ReadAllText(_settingsFileName);
            return JsonConvert.DeserializeObject<SettingsResponse>(json) ?? new();
        }

        var settings = new SettingsResponse();
        File.WriteAllText(_settingsFileName, JsonConvert.SerializeObject(settings));
        return settings;
    }

    private async Task SaveSettingsAsync()
    {
        var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
        await File.WriteAllTextAsync(_settingsFileName, json);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task<string> GetRevitPathAsync()
    {
        return Task.FromResult(_settings.Settings.RevitPath);
    }

    public async Task SetRevitPathAsync(string path)
    {
        _settings.Settings.RevitPath = path;
        await SaveSettingsAsync();
    }

    public Task<string> GetSelectedVersionAsync()
    {
        return Task.FromResult(_settings.Settings.SelectedVersion);
    }

    public async Task SetSelectedVersionAsync(string version)
    {
        _settings.Settings.SelectedVersion = version;
        await SaveSettingsAsync();
    }
}
