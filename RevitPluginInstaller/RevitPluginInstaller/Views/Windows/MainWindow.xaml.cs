using RevitPluginInstaller.Managers.Bases.Theme;
using RevitPluginInstaller.ViewModels.Windows;

namespace RevitPluginInstaller.Views.Windows;

public partial class MainWindow : WindowThemeBase
{
    public MainWindowViewModel ViewModel { get; }

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();

        ViewModel = viewModel;
        DataContext = this;
        ViewModel.SetMainFrame(MainFrame);
    }
}
