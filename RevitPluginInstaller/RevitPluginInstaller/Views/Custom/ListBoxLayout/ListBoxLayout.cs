using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;

namespace RevitPluginInstaller.Views.Custom.ListBoxLayout;

public class ListBoxLayout : ListBox
{
    private const int MinUpdateIntervalMs = 300;
    private DateTime _lastUpdateTime;
    private FrameworkElement? _parent;

    public ListBoxLayout()
    {
        Loaded += CustomScrollableListBox_Loaded;
    }

    private void CustomScrollableListBox_Loaded(object sender, RoutedEventArgs e)
    {
        _parent = VisualTreeHelper.GetParent(this) as FrameworkElement;

        if (_parent is not null)
            _parent.SizeChanged += Parent_SizeChanged;

        UpdateMaxHeight();
    }

    private void Parent_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateMaxHeight();
    }

    protected override void OnItemsChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        UpdateMaxHeight();
    }

    private void UpdateMaxHeight()
    {
        if (_parent is null || Items.Count == 0)
        {
            ClearValue(MaxHeightProperty);
            return;
        }

        if ((DateTime.Now - _lastUpdateTime).TotalMilliseconds < MinUpdateIntervalMs)
        {
            return;
        }

        double availableHeight = _parent.ActualHeight;
        if (double.IsNaN(availableHeight) || availableHeight <= 0)
        {
            availableHeight = _parent.RenderSize.Height;
        }

        if (availableHeight > 0)
        {
            double adjustedHeight = Math.Max(0, availableHeight - 10);
            SetValue(MaxHeightProperty, adjustedHeight);
        }
        else
        {
            ClearValue(MaxHeightProperty);
        }

        _lastUpdateTime = DateTime.Now;
    }
}