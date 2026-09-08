using Microsoft.Extensions.DependencyInjection;
using RevitPluginInstaller.ViewModels.Windows;
using RevitPluginInstaller.Services.Abstracts;
using RevitPluginInstaller.Managers.Abstracts;
using RevitPluginInstaller.ViewModels.Pages;
using RevitPluginInstaller.Managers.Bases;
using RevitPluginInstaller.Services.Bases;
using RevitPluginInstaller.Views.Windows;
using RevitPluginInstaller.Views.Pages;

namespace RevitPluginInstaller.Extensions;

public static class PersistenceExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services
            .AddSingleton<ILoggerManager, LoggerManager>()
            .AddSingleton<PageManager>();

        services
            .AddSingleton<ISettingsService, SettingsService>()
            .AddSingleton<IPluginService, PluginService>()
            .AddSingleton<IFileService, FileService>();

        services
            .AddSingleton<MainWindow>();

        services
            .AddTransient<DownloadPage>()
            .AddTransient<BackupsPage>()
            .AddTransient<TransferPage>()
            .AddTransient<SettingPage>();

        services
            .AddTransient<MainWindowViewModel>()
            .AddTransient<DownloadViewModel>()
            .AddTransient<BackupsViewModel>()
            .AddTransient<TransferViewModel>()
            .AddTransient<SettingsViewModel>();

        return services;
    }
}
