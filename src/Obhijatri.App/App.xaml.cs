using System.Diagnostics;
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
        Show(new MainWindow(isPrivate: false, LoadStartupSession()));
    }

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
        window.Closed += (_, _) =>
        {
            Windows.Remove(window);
            if (Windows.Count == 0)
            {
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
