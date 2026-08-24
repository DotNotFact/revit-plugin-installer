using Microsoft.Extensions.DependencyInjection;
using RevitPluginInstaller.Managers.Abstracts;
using System.Windows.Controls;

namespace RevitPluginInstaller.Managers.Bases;

public class PageManager(IServiceProvider serviceProvider) : IPageManager
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private Frame? _frame;

    public Page ActivePage => _frame?.Content as Page ?? new Page();

    public void Initialization(Frame frame)
    {
        if (_frame is not null)
            return;

        _frame = frame;
    }

    public void Navigate<T>() where T : class
    {
        if (_frame is null)
            throw new InvalidOperationException("Frame is not initialized. Call Initialization method first.");

        _frame.Content = _serviceProvider.GetRequiredService<T>();
    }

    public void Navigate(Type pageType)
    {
        if (_frame is null)
            throw new InvalidOperationException("Frame is not initialized. Call Initialization method first.");

        _frame.Content = (Page)_serviceProvider.GetRequiredService(pageType);
    }
}