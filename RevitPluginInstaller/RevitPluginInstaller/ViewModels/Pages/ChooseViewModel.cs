using RevitPluginInstaller.Infrastructure.Comands.Base;
using RevitPluginInstaller.Services.Abstracts;
using RevitPluginInstaller.ViewModels.Base;
using RevitPluginInstaller.Managers.Bases;
using RevitPluginInstaller.Views.Pages;
using System.Windows.Input;
using System.IO;

namespace RevitPluginInstaller.ViewModels.Pages;

public class ChooseViewModel : ViewModel
{
    #region [ Services ]

    private readonly ISettingsService _settingsService;
    private readonly IPluginService _pluginService;

    #endregion

    #region [ Managers ]

    private readonly PageManager _pageManager;

    #endregion

    #region [ Variables ]

    #region [ RevitVersions ]

    public IEnumerable<string> RevitVersions { get; private set; } = [];

    #endregion

    #region [ SelectedVersion ]

    private string _selectedVersion = string.Empty;
    public string SelectedVersion
    {
        get => _selectedVersion;
        set => Set(ref _selectedVersion, value);
    }

    #endregion

    #region [ Heading ]

    private readonly string _heading = "Вас приветствует установщик плагинов. Выберите версию Revit:";
    public string Heading
    {
        get => _heading;
    }

    #endregion

    #endregion

    #region [ Commands ]

    #region [ SelectVersionCommand ]

    public ICommand SelectVersionCommand { get; }

    private async void OnSelectVersionExecuteAsync(object? p)
    {
        await _settingsService.SetSelectedVersionAsync(SelectedVersion);
        _pageManager.Navigate<DownloadPage>();

    }

    private bool CanSelectVersionExecute(object? p)
    {
        if (!string.IsNullOrEmpty(SelectedVersion))
            return true;

        return false;
    }

    #endregion

    #endregion

    public ChooseViewModel(ISettingsService settingsService, IPluginService pluginService, PageManager pageManager)
    {
        _settingsService = settingsService;
        _pluginService = pluginService;
        _pageManager = pageManager;

        SelectVersionCommand = new ActionCommand(OnSelectVersionExecuteAsync, CanSelectVersionExecute);

        LoadRevitVersions();
    }

    private void LoadRevitVersions()
    {
        var revitPath = _settingsService.GetRevitPathAsync().GetAwaiter().GetResult();

        if (string.IsNullOrEmpty(revitPath) || !Directory.Exists(Path.Combine(revitPath, "Addins")))
        {
            RevitVersions = [];
            return;
        }

        RevitVersions = _pluginService.GetAvailableRevitVersions(revitPath);
    }
}