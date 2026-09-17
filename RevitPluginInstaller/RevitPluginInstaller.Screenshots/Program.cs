using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using RevitPluginInstaller.Extensions;
using RevitPluginInstaller.Managers.Bases.Theme;
using RevitPluginInstaller.Models;
using RevitPluginInstaller.ViewModels.Core;
using RevitPluginInstaller.ViewModels.Pages;
using RevitPluginInstaller.Views.Pages;
using RevitPluginInstaller.Views.Windows;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace RevitPluginInstaller.Screenshots;

/// <summary>
/// Renders the main window off-screen against a demo Revit folder and writes PNGs for the README.
/// Usage: dotnet run --project RevitPluginInstaller.Screenshots -- [outputDir] [scale]
/// </summary>
internal static class Program
{
    private const int Width = 1400;
    private const int Height = 880;

    [STAThread]
    private static int Main(string[] args)
    {
        var outputDir = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "shots"));
        var scale = args.Length > 1 && double.TryParse(args[1], System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 1.5;

        Directory.CreateDirectory(outputDir);

        // A realistic-looking, user-independent location; removed again when the run finishes.
        var demoRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "Autodesk", "Revit");
        DemoData.Build(demoRoot);

        // Settings live next to the executable, so the real user profile is never touched.
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "settings.json"),
            JsonConvert.SerializeObject(new SettingsResponse { Settings = new Settings { RevitPath = demoRoot, SelectedVersion = "2026" } }));

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/RevitPluginInstaller;component/Resources/AppResources.xaml", UriKind.Absolute)
        });
        ThemeManager.ApplyTheme(Theme.Dark);

        var services = new ServiceCollection().AddPersistence().BuildServiceProvider();
        var window = services.GetRequiredService<MainWindow>();

        window.Width = Width;
        window.Height = Height;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -20000;
        window.Top = -20000;
        window.ShowInTaskbar = false;
        window.ShowActivated = false;

        var exitCode = 0;

        window.Loaded += async (_, _) =>
        {
            try
            {
                await Idle(window);

                // 1. Installed - with one queued file and an installation in flight
                var installed = (DownloadPage)window.MainFrame.Content;
                installed.ViewModel.DropCommand.Execute(new DataObject(DataFormats.FileDrop, new[] { DemoData.PendingFile(demoRoot) }));
                await Idle(window);
                SimulateProgress(installed.ViewModel, "Установка MEP Toolkit 2.2 - копирование файлов…", 72);
                await Idle(window);
                Capture(window, Path.Combine(outputDir, "01-installed.png"), scale);

                // 2. Backups
                window.ViewModel.Navigate(Section.Backups);
                await Idle(window);
                Capture(window, Path.Combine(outputDir, "02-backups.png"), scale);

                // 3. Export / import
                window.ViewModel.Navigate(Section.Transfer);
                await Idle(window);
                Capture(window, Path.Combine(outputDir, "03-transfer.png"), scale);

                // 4. Settings, light theme
                window.ViewModel.Navigate(Section.Settings);
                await Idle(window);
                ((SettingPage)window.MainFrame.Content).ViewModel.IsDarkTheme = false;
                await Idle(window);
                Capture(window, Path.Combine(outputDir, "04-settings-light.png"), scale);

                Console.WriteLine($"Screenshots written to {outputDir}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                exitCode = 1;
            }
            finally
            {
                try { Directory.Delete(demoRoot, true); } catch (IOException) { }
                app.Shutdown();
            }
        };

        window.Show();
        app.Run();

        return exitCode;
    }

    private static Task Idle(Window window)
    {
        var tcs = new TaskCompletionSource();
        window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
        {
            window.UpdateLayout();
            tcs.SetResult();
        });
        return tcs.Task;
    }

    private static void SimulateProgress(DownloadViewModel vm, string status, int percent)
    {
        vm.StatusText = status;
        vm.InstallationProgress = percent;

        var field = typeof(DownloadViewModel).GetField("_isBusy", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(vm, true);

        var raise = typeof(DownloadViewModel).GetMethod("OnPropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
        raise.Invoke(vm, [nameof(DownloadViewModel.IsBusy)]);
    }

    private static void Capture(Window window, string path, double scale)
    {
        var root = VisualTreeHelper.GetChildrenCount(window) > 0 ? (Visual)VisualTreeHelper.GetChild(window, 0) : window;

        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(window.ActualWidth * scale),
            (int)Math.Ceiling(window.ActualHeight * scale),
            96 * scale, 96 * scale, PixelFormats.Pbgra32);

        bitmap.Render(root);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = File.Create(path);
        encoder.Save(stream);

        Console.WriteLine($"saved {Path.GetFileName(path)} ({bitmap.PixelWidth}x{bitmap.PixelHeight})");
    }
}

/// <summary>Creates a throw-away Revit folder with Addins per version, a plugin registry and a few backups.</summary>
internal static class DemoData
{
    private static readonly Random Rng = new(20260908);

    public static void Build(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);

        var addins = Path.Combine(root, "Addins");
        var packs = new List<PluginPack>();

        var v2026 = new (string file, double mb, string date)[]
        {
            ("WallFramer.addin", 4.2, "2026-08-12"),
            ("SmartDim.dll", 1.8, "2026-08-03"),
            ("MepToolkit.addin", 12.6, "2026-07-29"),
            ("SchedExport.dll", 0.87, "2026-07-21"),
            ("RebarDetailer.addin", 6.4, "2026-07-15"),
            ("FamBrowser.dll", 3.1, "2026-07-02"),
            ("ClashCheck.addin", 9.3, "2026-06-18"),
            ("RoomTagger.dll", 1.2, "2026-06-05"),
            ("SheetSetup.addin", 2.7, "2026-05-27"),
            ("ParamSync.dll", 0.64, "2026-05-14"),
            ("LevelTools.addin", 1.9, "2026-04-30"),
            ("ViewFilters.dll", 2.2, "2026-04-09"),
        };

        var v2025 = new (string file, double mb, string date)[]
        {
            ("WallFramer.addin", 4.0, "2025-11-02"), ("SmartDim.dll", 1.7, "2025-10-11"), ("MepToolkit.addin", 11.9, "2025-09-30"),
            ("SchedExport.dll", 0.8, "2025-09-12"), ("RebarDetailer.addin", 6.1, "2025-08-20"), ("FamBrowser.dll", 2.9, "2025-08-01"),
            ("ClashCheck.addin", 8.8, "2025-07-14"), ("RoomTagger.dll", 1.1, "2025-06-27"), ("ParamSync.dll", 0.6, "2025-06-03"),
        };

        var v2024 = new (string file, double mb, string date)[]
        {
            ("WallFramer.addin", 3.6, "2024-12-19"), ("SmartDim.dll", 1.5, "2024-11-08"), ("SchedExport.dll", 0.7, "2024-10-22"), ("FamBrowser.dll", 2.4, "2024-09-15"),
        };

        foreach (var (version, plugins) in new[] { ("2026", v2026), ("2025", v2025), ("2024", v2024) })
        {
            var dir = Path.Combine(addins, version);
            Directory.CreateDirectory(dir);

            foreach (var (file, mb, date) in plugins)
            {
                var path = Path.Combine(dir, file);
                WriteBlob(path, (long)(mb * 1024 * 1024));

                packs.Add(new PluginPack
                {
                    Id = Guid.NewGuid(),
                    Name = file,
                    Version = version,
                    InstallationDate = DateTime.Parse(date),
                    Plugins = [new Plugin { Id = Guid.NewGuid(), Name = file, Link = path, IsDrop = false }],
                });
            }
        }

        // Older, empty version folder - shows up in the sidebar with a zero counter
        Directory.CreateDirectory(Path.Combine(addins, "2023"));

        File.WriteAllText(Path.Combine(root, "plugins.json"),
            JsonConvert.SerializeObject(new PluginResponse { PluginPacks = packs }, Formatting.Indented));

        var backups = Path.Combine(root, "backups");
        Backup(backups, "20260729141205", ("MepToolkit.addin", 11.9));
        Backup(backups, "20260812093311", ("WallFramer.addin", 4.0));
        Backup(backups, "20260901181742", ("SmartDim.dll", 1.7), ("SchedExport.dll", 0.8));

        File.WriteAllText(Path.Combine(root, "RevitPluginInstaller.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Demo data generated{Environment.NewLine}");
    }

    public static string PendingFile(string root)
    {
        var downloads = Path.Combine(root, "Downloads", "MepToolkit-2.2");
        Directory.CreateDirectory(downloads);
        var path = Path.Combine(downloads, "MepToolkit.addin");
        WriteBlob(path, (long)(12.8 * 1024 * 1024));
        return path;
    }

    private static void Backup(string root, string stamp, params (string file, double mb)[] files)
    {
        var dir = Path.Combine(root, stamp);
        Directory.CreateDirectory(dir);
        foreach (var (file, mb) in files)
            WriteBlob(Path.Combine(dir, file), (long)(mb * 1024 * 1024));
    }

    private static void WriteBlob(string path, long size)
    {
        using var stream = File.Create(path);
        stream.SetLength(size);
    }
}
