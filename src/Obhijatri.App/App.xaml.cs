using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Obhijatri.App.Localization;
using Obhijatri.App.Services;
using Obhijatri.Core;

namespace Obhijatri.App;

public partial class App : Application
{
    private static readonly List<MainWindow> Windows = [];
    private static DispatcherQueue? _uiQueue;

    public App()
    {
        // Settings first: the UI language decides which strings and fonts load.
        AppServices.Initialize();

        // Makes WinUI's own built-in text (for example control tooltips) follow the app language
        // instead of the Windows display language.
        Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = AppServices.Settings.UiLanguage;
        InitializeComponent();
        UnhandledException += App_UnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _uiQueue = DispatcherQueue.GetForCurrentThread();
        AppFonts.Apply(Resources, AppServices.Settings.IsBangla);
        FilterService.Start(_uiQueue);
        ScamShieldService.Start(_uiQueue);
#if DEBUG
        if (BenchmarkRequested)
        {
            var benchmark = NewSelfTestWindow();
            Show(benchmark);
            _ = benchmark.RunBenchmarkAsync();
            return;
        }
        if (Environment.GetCommandLineArgs().Contains("--https-selftest", StringComparer.Ordinal))
        {
            var selfTest = NewSelfTestWindow();
            Show(selfTest);
            _ = selfTest.RunHttpsSelfTestAsync();
            return;
        }
        if (Environment.GetCommandLineArgs().Contains("--scamshield-selftest", StringComparer.Ordinal))
        {
            var selfTest = NewSelfTestWindow();
            Show(selfTest);
            _ = selfTest.RunScamShieldSelfTestAsync();
            return;
        }
        if (Environment.GetCommandLineArgs().Contains("--downloads-selftest", StringComparer.Ordinal))
        {
            var selfTest = NewSelfTestWindow();
            Show(selfTest);
            _ = selfTest.RunDownloadsSelfTestAsync();
            return;
        }
        if (Environment.GetCommandLineArgs().Contains("--perf-selftest", StringComparer.Ordinal))
        {
            var selfTest = NewSelfTestWindow();
            Show(selfTest);
            _ = selfTest.RunPerfSelfTestAsync();
            return;
        }
        if (Environment.GetCommandLineArgs().Contains("--lowdata-selftest", StringComparer.Ordinal))
        {
            var selfTest = NewSelfTestWindow();
            Show(selfTest);
            _ = selfTest.RunLowDataSelfTestAsync();
            return;
        }
        if (Environment.GetCommandLineArgs().Contains("--about-selftest", StringComparer.Ordinal))
        {
            var selfTest = NewSelfTestWindow();
            Show(selfTest);
            _ = selfTest.RunAboutSelfTestAsync();
            return;
        }
        if (Environment.GetCommandLineArgs().Contains("--translate-selftest", StringComparer.Ordinal))
        {
            var selfTest = NewSelfTestWindow();
            Show(selfTest);
            _ = selfTest.RunTranslateSelfTestAsync();
            return;
        }
        if (Environment.GetCommandLineArgs().Contains("--scamcheck-selftest", StringComparer.Ordinal))
        {
            var selfTest = NewSelfTestWindow();
            Show(selfTest);
            _ = selfTest.RunScamCheckSelfTestAsync();
            return;
        }
        if (Environment.GetCommandLineArgs().Contains("--reader-selftest", StringComparer.Ordinal))
        {
            var selfTest = NewSelfTestWindow();
            Show(selfTest);
            _ = selfTest.RunReaderSelfTestAsync();
            return;
        }
        if (Environment.GetCommandLineArgs().Contains("--newtab-selftest", StringComparer.Ordinal))
        {
            var selfTest = new MainWindow(isPrivate: true);
            Show(selfTest);
            _ = selfTest.RunNewTabSelfTestAsync();
            return;
        }
        if (Environment.GetCommandLineArgs().Contains("--theme-selftest", StringComparer.Ordinal))
        {
            var selfTest = NewSelfTestWindow();
            Show(selfTest);
            _ = selfTest.RunThemeSelfTestAsync();
            return;
        }
#endif
        Show(new MainWindow(isPrivate: false, LoadStartupSession()));
    }

#if DEBUG
    /// <summary>A private window that starts on a blank web page (not the new tab page), for the developer self-tests.</summary>
    private static MainWindow NewSelfTestWindow() =>
        new(isPrivate: true, [new Obhijatri.Core.Storage.SessionTab("about:blank", string.Empty, IsActive: true)]);
#endif

    private static IReadOnlyList<Obhijatri.Core.Storage.SessionTab> LoadStartupSession() =>
        AppServices.Settings.RestoreTabs ? AppServices.Sessions.Load() : [];

    /// <summary>
    /// Obhijatri was started again while already running. Bring the normal window to the front with
    /// a new tab, or open a normal window if only private ones are open. Called off the UI thread.
    /// </summary>
    public static void OnActivatedFromAnotherLaunch()
    {
        _uiQueue?.TryEnqueue(() =>
        {
            var normal = Windows.FirstOrDefault(w => !w.IsPrivate);
            if (normal is null)
            {
                Show(new MainWindow(isPrivate: false, LoadStartupSession()));
                return;
            }
            normal.BringToFrontWithNewTab();
        });
    }

    /// <summary>Per-site typing changed: update every open tab of that kind of window.</summary>
    internal static void NotifySitePhonetic(string? host, bool enabled, bool isPrivate)
    {
        foreach (var window in Windows.Where(w => w.IsPrivate == isPrivate))
        {
            window.NotifySitePhonetic(host, enabled);
        }
    }

    /// <summary>A private window: separate in-memory engine profile, no history, no saved tabs.</summary>
    public static void OpenPrivateWindow() => Show(new MainWindow(isPrivate: true));

    private static bool BenchmarkRequested =>
#if DEBUG
        Environment.GetCommandLineArgs().Contains("--benchmark", StringComparer.Ordinal);
#else
        false;
#endif

    /// <summary>Saves the open tabs and starts Obhijatri again (used after changing the language).</summary>
    public static void Restart()
    {
        foreach (var window in Windows)
        {
            window.SaveSessionNow();
        }
        AppServices.Database.Dispose();

        // Give up the single-instance key so the new process does not hand over to this one.
        AppInstance.GetCurrent().UnregisterKey();
        AppInstance.Restart(string.Empty);

        // Restart only returns if it failed: start a new process ourselves.
        if (Environment.ProcessPath is { } path)
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = false });
        }
        Environment.Exit(0);
    }

    private static void Show(MainWindow window)
    {
        Windows.Add(window);
        window.Closed += async (_, _) =>
        {
            Windows.Remove(window);
            if (Windows.Count == 0)
            {
                if (AppServices.Settings.CookieAutoDeleteEnabled && AppServices.NormalProfile is { } profile)
                {
                    try
                    {
                        await CookieAutoDelete.RunAsync(profile);
                    }
                    catch (Exception ex) when (ex is COMException or InvalidOperationException)
                    {
                        // Nothing left to clean up if the engine is already tearing down.
                    }
                }
                AppServices.Database.Dispose();
                Current.Exit();
            }
        };
        window.Activate();
    }

    // Local crash log only. Nothing is ever sent anywhere.
    private static void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LogFolder);
            File.AppendAllText(
                Path.Combine(AppPaths.LogFolder, "crash.log"),
                $"[{DateTimeOffset.Now:O}] {e.Exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Logging must never cause a second failure.
        }
    }
}
