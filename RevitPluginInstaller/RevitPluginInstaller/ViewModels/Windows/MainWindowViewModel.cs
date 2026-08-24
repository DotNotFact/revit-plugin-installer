using RevitPluginInstaller.Infrastructure.Comands.Base;
using RevitPluginInstaller.Infrastructure.Comands;
using RevitPluginInstaller.Managers.Bases.Theme;
using RevitPluginInstaller.ViewModels.Base;
using RevitPluginInstaller.Managers.Bases;
using RevitPluginInstaller.Views.Pages;
using System.Windows.Controls;
using System.Windows.Input;

namespace RevitPluginInstaller.ViewModels.Windows;

public class MainWindowViewModel : ViewModel
{
    #region [ Managers ]

    private readonly PageManager _pageManager;

    #endregion

    #region [ Variables ]

    #region [ Title ]

    private readonly string _title = "RevitPluginInstaller";
    public string Title
    {
        get => _title;
    }

    #endregion

    #endregion

    #region [ Commands ]

    #region [ ToggleThemeCommand ]

    private Theme _currentTheme = Theme.Dark;
    public ICommand ToggleThemeCommand { get; }

    private void OnToggleThemeCommandExecute(object? p)
    {
        _currentTheme = _currentTheme is Theme.Dark ? Theme.Light : Theme.Dark;
        ThemeManager.ApplyTheme(_currentTheme);
    }

    #endregion

    #region [ MaximizeMinimizeCommand ]

    // public ICommand MaximizeMinimizeCommand { get; } 
    // MaximizeMinimizeCommand = new MaximizeMinimizeApplicationCommand();

    #endregion

    #region [ CloseApplicationCommand ]

    public ICommand CloseApplicationCommand { get; }

    #endregion

    #region [ NavigateCommand ]

    public ICommand NavigateCommand { get; }

    private void OnNavigateCommandExecute(Type pageType)
    {
        _pageManager.Navigate(pageType);
    }

    #endregion

    #endregion

    public MainWindowViewModel(PageManager pageManager)
    {
        _pageManager = pageManager;

        CloseApplicationCommand = new CloseApplicationCommand();
        NavigateCommand = new NavigateCommand(OnNavigateCommandExecute);
        ToggleThemeCommand = new ActionCommand(OnToggleThemeCommandExecute);
    }

    public void SetMainFrame(Frame mainFrame)
    {
        _pageManager.Initialization(mainFrame);
        _pageManager.Navigate<ChoosePage>();
    }
}