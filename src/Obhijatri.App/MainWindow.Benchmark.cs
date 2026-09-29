#if DEBUG
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Web.WebView2.Core;
using Obhijatri.App.Browser;
using Obhijatri.App.Services;
using Obhijatri.Core;

namespace Obhijatri.App;

/// <summary>
/// Developer-only page load benchmark (Debug builds, started with --benchmark). Runs in a private
/// window so nothing is added to history. Each site is loaded with blocking off and on, the order
/// alternating between rounds, with the cache cleared before every load. Results go to
/// %LOCALAPPDATA%\Obhijatri\logs\benchmark.txt and the app then exits.
/// </summary>
public sealed partial class MainWindow
{
    private static readonly string[] BenchmarkSites =
    [
        "https://www.prothomalo.com/",
        "https://www.kalerkantho.com/",
        "https://www.jugantor.com/",
        "https://bdnews24.com/",
        "https://www.thedailystar.net/",
    ];

    private const int BenchmarkRounds = 3;

    internal async Task RunBenchmarkAsync()
    {
        var log = new StringBuilder();
        var rows = new List<(string Site, bool Blocking, double Ms, int Blocked, int Requests)>();
        var thirdParty = new Dictionary<string, List<string>>();
        try
        {
            for (var wait = 0; FilterService.Engine.RuleCount == 0 && wait < 150; wait++)
            {
                await Task.Delay(200);
            }

            var tab = _activeTab;
            for (var wait = 0; tab?.WebView?.CoreWebView2 is null && wait < 150; wait++)
            {
                await Task.Delay(200);
            }
            var core = tab!.WebView!.CoreWebView2;

            log.AppendLine(CultureInfo.InvariantCulture, $"Obhijatri page load benchmark {DateTimeOffset.Now:yyyy-MM-dd HH:mm}");
            log.AppendLine(CultureInfo.InvariantCulture, $"Filter rules: {FilterService.Engine.RuleCount}, rounds: {BenchmarkRounds}, cache cleared before every load");
            log.AppendLine("site,blocking,round,load_ms,blocked,requests");

            for (var round = 0; round < BenchmarkRounds; round++)
            {
                foreach (var site in BenchmarkSites)
                {
                    bool[] modes = round % 2 == 0 ? [false, true] : [true, false];
                    foreach (var blocking in modes)
                    {
                        FilterService.SetEnabledForBenchmark(blocking);
                        await core.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.DiskCache | CoreWebView2BrowsingDataKinds.CacheStorage);
                        _ = await NavigateAndWaitAsync(core, "about:blank");
                        tab.ResetRequestCount();

                        var watch = Stopwatch.StartNew();
                        var (ok, status) = await NavigateAndWaitAsync(core, site);
                        watch.Stop();

                        var ms = ok ? watch.Elapsed.TotalMilliseconds : double.NaN;
                        if (!ok)
                        {
                            log.AppendLine(CultureInfo.InvariantCulture, $"# load failed: {site} blocking={blocking} status={status}");
                        }
                        if (round == 0)
                        {
                            await Task.Delay(2500);
                            await CapturePageAsync(core, site, blocking);
                            if (blocking)
                            {
                                thirdParty[site] = tab.PassedThirdPartyHosts.Order().ToList();
                            }
                        }
                        rows.Add((site, blocking, ms, tab.BlockedCount, tab.RequestCount));
                        log.AppendLine(CultureInfo.InvariantCulture, $"{site},{(blocking ? "on" : "off")},{round + 1},{ms:F0},{tab.BlockedCount},{tab.RequestCount}");
                        await Task.Delay(1500);
                    }
                }
            }

            log.AppendLine();
            log.AppendLine("Median load time (ms) per site:");
            double totalOff = 0, totalOn = 0;
            foreach (var site in BenchmarkSites)
            {
                var off = Median(rows.Where(r => r.Site == site && !r.Blocking).Select(r => r.Ms));
                var on = Median(rows.Where(r => r.Site == site && r.Blocking).Select(r => r.Ms));
                var blocked = Median(rows.Where(r => r.Site == site && r.Blocking).Select(r => (double)r.Blocked));
                var requestsOff = Median(rows.Where(r => r.Site == site && !r.Blocking).Select(r => (double)r.Requests));
                var requestsOn = Median(rows.Where(r => r.Site == site && r.Blocking).Select(r => (double)r.Requests));
                totalOff += off;
                totalOn += on;
                log.AppendLine(CultureInfo.InvariantCulture,
                    $"  {site,-32} off {off,7:F0}  on {on,7:F0}  change {100 * (on - off) / off,6:F1}%  blocked {blocked,4:F0}  requests {requestsOff:F0} -> {requestsOn:F0}");
            }
            log.AppendLine(CultureInfo.InvariantCulture, $"  {"all sites",-32} off {totalOff,7:F0}  on {totalOn,7:F0}  change {100 * (totalOn - totalOff) / totalOff,6:F1}%");
            log.AppendLine(CultureInfo.InvariantCulture,
                $"Filter check time: {BrowserTab.FilterCheckMicroseconds:F1} µs per request on average ({BrowserTab.FilterChecks} requests checked)");
            log.AppendLine();
            log.AppendLine("Third-party hosts that were not blocked (round 1, blocking on):");
            foreach (var (site, hosts) in thirdParty)
            {
                log.AppendLine(CultureInfo.InvariantCulture, $"  {site}: {string.Join(", ", hosts)}");
            }
        }
        catch (Exception ex)
        {
            log.AppendLine("Benchmark failed: " + ex); // not-ui
        }
        finally
        {
            FilterService.SetEnabledForBenchmark(AppServices.Settings.BlockAds);
            Directory.CreateDirectory(AppPaths.LogFolder);
            await File.WriteAllTextAsync(Path.Combine(AppPaths.LogFolder, "benchmark.txt"), log.ToString());
            Microsoft.UI.Xaml.Application.Current.Exit();
        }
    }

    private static async Task<(bool Ok, string Status)> NavigateAndWaitAsync(CoreWebView2 core, string url)
    {
        var done = new TaskCompletionSource<(bool, string)>();
        void Completed(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args) =>
            done.TrySetResult((args.IsSuccess, args.WebErrorStatus.ToString()));
        core.NavigationCompleted += Completed;
        try
        {
            core.Navigate(url);
            var finished = await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromSeconds(60)));
            return finished == done.Task ? done.Task.Result : (false, "Timeout60s");
        }
        finally
        {
            core.NavigationCompleted -= Completed;
        }
    }

    private static async Task CapturePageAsync(CoreWebView2 core, string site, bool blocking, string folderName = "benchmark")
    {
        var folder = Path.Combine(AppPaths.LogFolder, folderName);
        Directory.CreateDirectory(folder);
        var name = new Uri(site).Host.Replace("www.", string.Empty, StringComparison.Ordinal) + (blocking ? "-on" : "-off") + ".png";
        await using var file = File.Create(Path.Combine(folder, name));
        await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, file.AsRandomAccessStream());
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.Where(v => !double.IsNaN(v)).Order().ToArray();
        return sorted.Length == 0 ? double.NaN
            : sorted.Length % 2 == 1 ? sorted[sorted.Length / 2]
            : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
    }
}
#endif
