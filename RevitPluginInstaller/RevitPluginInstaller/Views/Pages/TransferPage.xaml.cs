using RevitPluginInstaller.ViewModels.Pages;
using System.Windows.Controls;

namespace RevitPluginInstaller.Views.Pages;

public partial class TransferPage : Page
{
    public TransferViewModel ViewModel { get; }

    public TransferPage(TransferViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
