using System.Windows;

namespace RevitPluginInstaller.Managers.Bases.Theme;

public static class ThemeManager
{
    private static ResourceDictionary? _currentDictionary;

    public static Theme CurrentTheme { get; private set; } = Theme.Dark;

    public static void ApplyTheme(Theme theme)
    {
        var app = Application.Current;
        if (app is null)
            return;

        var mergedDicts = app.Resources.MergedDictionaries;

        if (_currentDictionary is not null)
            mergedDicts.Remove(_currentDictionary);

        _currentDictionary = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/RevitPluginInstaller;component/Resources/Themes/{theme}.xaml", UriKind.Absolute)
        };
        mergedDicts.Add(_currentDictionary);
        CurrentTheme = theme;

        foreach (Window window in app.Windows)
            if (window is IThemedWindow themedWindow)
                themedWindow.ApplyTheme(theme);
    }

    public static void Toggle() => ApplyTheme(CurrentTheme == Theme.Dark ? Theme.Light : Theme.Dark);
}
