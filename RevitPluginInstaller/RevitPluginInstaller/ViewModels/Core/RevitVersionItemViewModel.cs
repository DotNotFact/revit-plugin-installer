using RevitPluginInstaller.ViewModels.Base;

namespace RevitPluginInstaller.ViewModels.Core;

public class RevitVersionItemViewModel : ViewModel
{
    public required string Version { get; init; }

    public string DisplayName => $"Revit {Version}";

    private int _pluginCount;
    public int PluginCount
    {
        get => _pluginCount;
        set
        {
            if (Set(ref _pluginCount, value))
            {
                OnPropertyChanged(nameof(HasPlugins));
                OnPropertyChanged(nameof(CountText));
            }
        }
    }

    public bool HasPlugins => PluginCount > 0;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (Set(ref _isSelected, value))
                OnPropertyChanged(nameof(CountText));
        }
    }

    /// <summary>"12 плагинов" for the selected version, a bare number for the rest.</summary>
    public string CountText => IsSelected ? $"{PluginCount} {Pluralize(PluginCount)}" : PluginCount.ToString();

    public static string Pluralize(int count)
    {
        var mod10 = count % 10;
        var mod100 = count % 100;

        if (mod10 == 1 && mod100 != 11) return "плагин";
        if (mod10 is >= 2 and <= 4 && mod100 is < 12 or > 14) return "плагина";
        return "плагинов";
    }
}
