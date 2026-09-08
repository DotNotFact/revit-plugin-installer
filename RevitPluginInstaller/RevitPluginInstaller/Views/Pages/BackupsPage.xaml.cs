using RevitPluginInstaller.ViewModels.Pages;
using System.Windows.Controls;

namespace RevitPluginInstaller.Views.Pages;

public partial class BackupsPage : Page
{
    public BackupsViewModel ViewModel { get; }

    public BackupsPage(BackupsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
