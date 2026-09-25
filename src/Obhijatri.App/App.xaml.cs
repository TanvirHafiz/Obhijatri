using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Obhijatri.App.Services;
using Obhijatri.Core;

namespace Obhijatri.App;

public partial class App : Application
{
    private static readonly List<MainWindow> Windows = [];
    private static DispatcherQueue? _uiQueue;

    public App()
    {
        // Makes WinUI's own built-in text (for example control tooltips) follow the app language
        // instead of the Windows display language.
        Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = BrowserDefaults.UiLanguage;
        InitializeComponent();
        UnhandledException += App_UnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _uiQueue = DispatcherQueue.GetForCurrentThread();
        AppServices.Initialize();
        Show(new MainWindow(isPrivate: false, AppServices.Sessions.Load()));
    }

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
                Show(new MainWindow(isPrivate: false, AppServices.Sessions.Load()));
                return;
            }
            normal.BringToFrontWithNewTab();
        });
    }

    /// <summary>A private window: separate in-memory engine profile, no history, no saved tabs.</summary>
    public static void OpenPrivateWindow() => Show(new MainWindow(isPrivate: true));

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
