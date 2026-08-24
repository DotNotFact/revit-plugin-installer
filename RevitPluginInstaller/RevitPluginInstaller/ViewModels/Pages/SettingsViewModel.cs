using RevitPluginInstaller.Infrastructure.Comands.Base;
using RevitPluginInstaller.Services.Abstracts;
using RevitPluginInstaller.ViewModels.Base;
using System.Windows.Input;
using Microsoft.Win32;
using System.Windows;

namespace RevitPluginInstaller.ViewModels.Pages;

public class SettingsViewModel : ViewModel
{
    #region [ Services ]

    private readonly ISettingsService _settingsService;

    #endregion 

    #region [ Variables ] 

    #region [ RevitPath ]

    private string _revitPath = string.Empty;
    public string RevitPath
    {
        get => _revitPath;
        set => Set(ref _revitPath, value);
    }

    #endregion

    #region [ Heading ]

    private string _heading = "Настройки";
    public string Heading
    {
        get => _heading;
    }

    #endregion

    #endregion

    #region [ Commands ]

    #region [ SelectFolderCommand ]

    public ICommand SelectFolderCommand { get; }

    private async void OnSelectFolderCommandExecuteAsync(object? p)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Выберите папку с Revit",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            var path = dialog.FolderName.Split("\\").Last();

            if (!path.Contains("Revit"))
            {
                MessageBox.Show("Выберите папку с именем Revit");
                return;
            }

            RevitPath = dialog.FolderName;
            await _settingsService.SetRevitPathAsync(RevitPath);
        }
    }

    #endregion

    #endregion

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        SelectFolderCommand = new ActionCommand(OnSelectFolderCommandExecuteAsync);

        LoadSettings();
    }

    private void LoadSettings()
    {
        RevitPath = _settingsService.GetRevitPathAsync().GetAwaiter().GetResult();
    }
}