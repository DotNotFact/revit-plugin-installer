using RevitPluginInstaller.Infrastructure.Comands.Base;
using RevitPluginInstaller.Managers.Bases;
using RevitPluginInstaller.Services.Abstracts;
using RevitPluginInstaller.ViewModels.Base;
using RevitPluginInstaller.ViewModels.Core;
using RevitPluginInstaller.Views.Pages;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RevitPluginInstaller.ViewModels.Windows;

public class MainWindowViewModel : ViewModel
{
    #region [ DI ]

    private readonly PageManager _pageManager;
    private readonly ISettingsService _settingsService;
    private readonly IPluginService _pluginService;

    #endregion

    #region [ Header ]

    public string Title => "Revit Plugin Manager";

    private string _subtitle = string.Empty;
    public string Subtitle
    {
        get => _subtitle;
        private set => Set(ref _subtitle, value);
    }

    public string AppVersion { get; }

    #endregion

    #region [ Sidebar ]

    public ObservableCollection<RevitVersionItemViewModel> Versions { get; } = [];

    private bool _hasVersions;
    public bool HasVersions
    {
        get => _hasVersions;
        private set => Set(ref _hasVersions, value);
    }

    private Section _activeSection = Section.Installed;
    public Section ActiveSection
    {
        get => _activeSection;
        private set => Set(ref _activeSection, value);
    }

    #endregion

    #region [ Commands ]

    public ICommand SelectVersionCommand { get; }
    public ICommand NavigateSectionCommand { get; }
    public ICommand MinimizeCommand { get; }
    public ICommand MaximizeCommand { get; }
    public ICommand CloseApplicationCommand { get; }

    #endregion

    public MainWindowViewModel(PageManager pageManager, ISettingsService settingsService, IPluginService pluginService)
    {
        _pageManager = pageManager;
        _settingsService = settingsService;
        _pluginService = pluginService;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        AppVersion = version is null ? "v1.0" : $"v{version.Major}.{version.Minor}";

        SelectVersionCommand = new ActionCommand(OnSelectVersionAsync);
        NavigateSectionCommand = new ActionCommand(OnNavigateSection);
        MinimizeCommand = new ActionCommand(_ => { if (Application.Current.MainWindow is { } w) w.WindowState = WindowState.Minimized; });
        MaximizeCommand = new ActionCommand(_ =>
        {
            if (Application.Current.MainWindow is { } w)
                w.WindowState = w.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        });
        CloseApplicationCommand = new ActionCommand(_ => Application.Current.Shutdown());

        _pluginService.PluginsChanged += (_, _) => OnUiThread(RefreshVersions);
        _settingsService.SettingsChanged += (_, _) => OnUiThread(RefreshVersions);

        RefreshVersions();
    }

    public void SetMainFrame(Frame mainFrame)
    {
        _pageManager.Initialization(mainFrame);
        Navigate(HasVersions ? Section.Installed : Section.Settings);
    }

    #region [ Navigation ]

    private void OnNavigateSection(object? p)
    {
        var section = p switch
        {
            Section s => s,
            string text when Enum.TryParse<Section>(text, out var parsed) => parsed,
            _ => Section.Installed,
        };

        Navigate(section);
    }

    public void Navigate(Section section)
    {
        ActiveSection = section;

        switch (section)
        {
            case Section.Backups: _pageManager.Navigate<BackupsPage>(); break;
            case Section.Transfer: _pageManager.Navigate<TransferPage>(); break;
            case Section.Settings: _pageManager.Navigate<SettingPage>(); break;
            default: _pageManager.Navigate<DownloadPage>(); break;
        }
    }

    private async void OnSelectVersionAsync(object? p)
    {
        if (p is not RevitVersionItemViewModel item)
            return;

        var current = await _settingsService.GetSelectedVersionAsync();

        if (current != item.Version)
            await _settingsService.SetSelectedVersionAsync(item.Version);
        else
            RefreshVersions();

        Navigate(Section.Installed);
    }

    #endregion

    #region [ Versions ]

    private void RefreshVersions()
    {
        var revitPath = _settingsService.GetRevitPathAsync().GetAwaiter().GetResult();
        var selected = _settingsService.GetSelectedVersionAsync().GetAwaiter().GetResult();
        var versions = _pluginService.GetAvailableRevitVersions(revitPath).ToList();
        var packs = _pluginService.GetAllPluginsAsync().ToList();

        // Keep the same item instances so RadioButton state does not flicker
        var existing = Versions.ToDictionary(v => v.Version);
        var ordered = new List<RevitVersionItemViewModel>();

        foreach (var version in versions)
        {
            if (!existing.TryGetValue(version, out var item))
                item = new RevitVersionItemViewModel { Version = version };

            item.PluginCount = packs.Where(p => p.Version == version).Sum(p => p.Plugins.Count);
            item.IsSelected = version == selected;
            ordered.Add(item);
        }

        if (!ordered.SequenceEqual(Versions))
        {
            Versions.Clear();
            foreach (var item in ordered)
                Versions.Add(item);
        }

        HasVersions = Versions.Count > 0;
        Subtitle = BuildSubtitle(versions);
    }

    private string BuildSubtitle(IReadOnlyList<string> versions)
    {
        if (versions.Count == 0)
            return $"{AppVersion} · Autodesk Revit";

        var numeric = versions
            .Select(v => int.TryParse(v, out var year) ? year : (int?)null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .OrderBy(v => v)
            .ToList();

        if (numeric.Count == 0)
            return $"{AppVersion} · Autodesk Revit {versions[0]}";

        var range = numeric.Count == 1 || numeric[0] == numeric[^1]
            ? numeric[0].ToString()
            : $"{numeric[0]}–{numeric[^1]}";

        return $"{AppVersion} · Autodesk Revit {range}";
    }

    private static void OnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;

        if (dispatcher is null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.Invoke(action);
    }

    #endregion
}
