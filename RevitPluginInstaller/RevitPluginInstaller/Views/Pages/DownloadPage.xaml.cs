using RevitPluginInstaller.ViewModels.Pages;
using System.Windows;
using System.Windows.Controls;

namespace RevitPluginInstaller.Views.Pages;

public partial class DownloadPage : Page
{
    public DownloadViewModel ViewModel { get; }

    public DownloadPage(DownloadViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }

    private void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && ViewModel.DropCommand.CanExecute(e.Data))
            ViewModel.DropCommand.Execute(e.Data);
    }
}
