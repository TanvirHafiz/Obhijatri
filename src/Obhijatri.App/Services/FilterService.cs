using Microsoft.UI.Dispatching;
using Obhijatri.Core;
using Obhijatri.Core.Settings;
using Obhijatri.Safety.Filtering;

namespace Obhijatri.App.Services;

/// <summary>
/// Owns the ad and tracker filter engine. The engine is built on a background thread at startup
/// (pages load normally until it is ready) and rebuilt after a list update. Updates are checked a
/// few minutes after startup and then every few hours, and happen at most once a week.
/// </summary>
internal static class FilterService
{
    private const string BangladeshList = "bd-extra.txt";
    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    private static volatile FilterEngine _engine = FilterEngine.Empty;
    private static DispatcherQueue? _ui;
    private static DispatcherQueueTimer? _timer;
    private static HttpClient? _http;
    private static bool _updating;

    /// <summary>The current engine. Safe to read from any thread.</summary>
    public static FilterEngine Engine => _engine;

    /// <summary>Cached copy of the setting, so the per-request path never touches the database.</summary>
    public static bool Enabled { get; private set; }

    public static FilterListStore Store { get; } = new(
        AppPaths.FilterFolder, Path.Combine(AppContext.BaseDirectory, "Assets", "Filters"));

    /// <summary>Raised on the UI thread after the engine was (re)built.</summary>
    public static event EventHandler? Changed;

    public static void Start(DispatcherQueue ui)
    {
        _ui = ui;
        Enabled = AppServices.Settings.BlockAds;
        AppServices.Settings.Changed += (_, key) =>
        {
            if (key == BrowserSettings.Keys.BlockAds)
            {
                Enabled = AppServices.Settings.BlockAds;
            }
        };

        _ = Task.Run(Rebuild);

        _timer = ui.CreateTimer();
        _timer.Interval = FirstCheckDelay;
        _timer.Tick += async (timer, _) =>
        {
            timer.Interval = CheckInterval;
            if (Store.IsUpdateDue())
            {
                await UpdateNowAsync();
            }
        };
        _timer.Start();
    }

    /// <summary>Downloads the lists now. Returns how many lists were updated, or -1 if an update is already running.</summary>
    public static async Task<int> UpdateNowAsync()
    {
        if (_updating)
        {
            return -1;
        }

        _updating = true;
        try
        {
            _http ??= CreateHttpClient();
            using var cancel = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var updated = await Task.Run(() => Store.UpdateAsync(_http, cancel.Token));
            if (updated > 0)
            {
                await Task.Run(Rebuild);
            }
            return updated;
        }
        finally
        {
            _updating = false;
        }
    }

    private static void Rebuild()
    {
        var engine = FilterEngine.Build(Store.ReadAllLines([BangladeshList]));
        _engine = engine;
        _ui?.TryEnqueue(() => Changed?.Invoke(null, EventArgs.Empty));
    }

#if DEBUG
    /// <summary>Only for the developer benchmark.</summary>
    internal static void SetEnabledForBenchmark(bool enabled) => Enabled = enabled;
#endif

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Obhijatri-FilterUpdater/1.0");
        return http;
    }
}
