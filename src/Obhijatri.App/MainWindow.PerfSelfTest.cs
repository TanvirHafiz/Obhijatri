#if DEBUG
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Obhijatri.App.Browser;
using Obhijatri.App.Services;
using Obhijatri.Core;

namespace Obhijatri.App;

/// <summary>
/// Developer-only memory and tab sleeping self-test (Debug builds, --perf-selftest), in a private
/// window. Measures the idle shell, opens 20 real pages, then checks the idle-timer path, "sleep
/// now", the pinned-tab exemption, the memory saved and how fast a sleeping tab wakes. Results go
/// to %LOCALAPPDATA%\Obhijatri\logs\perf-selftest.txt and the app then exits.
/// </summary>
public sealed partial class MainWindow
{
    private static readonly string[] PerfSites =
    [
        "https://www.prothomalo.com/",
        "https://www.kalerkantho.com/",
        "https://www.jugantor.com/",
        "https://bdnews24.com/",
        "https://www.thedailystar.net/",
        "https://bn.wikipedia.org/wiki/%E0%A6%AC%E0%A6%BE%E0%A6%82%E0%A6%B2%E0%A6%BE%E0%A6%A6%E0%A7%87%E0%A6%B6",
        "https://en.wikipedia.org/wiki/Bangladesh",
        "https://www.bbc.com/bengali",
        "https://www.dhakatribune.com/",
        "https://www.tbsnews.net/",
        "https://www.banglatribune.com/",
        "https://www.ittefaq.com.bd/",
        "https://www.samakal.com/",
        "https://www.risingbd.com/",
        "https://www.dhakapost.com/",
        "https://www.jagonews24.com/",
        "https://www.ntvbd.com/",
        "https://news.ycombinator.com/",
        "https://www.python.org/",
        "https://example.com/",
    ];

    private static string Mb(long bytes) => (bytes / 1048576.0).ToString("F1", CultureInfo.InvariantCulture) + " MB";

    internal async Task RunPerfSelfTestAsync()
    {
        var log = new StringBuilder();
        try
        {
            var first = _activeTab!;
            for (var wait = 0; first.WebView?.CoreWebView2 is null && wait < 150; wait++)
            {
                await Task.Delay(200);
            }

            // 1. Idle: one blank tab.
            first.Navigate("about:blank");
            await Task.Delay(8000);
            var idle = await MemoryMeter.SampleAsync(_tabs);
            var self = Process.GetCurrentProcess();
            log.AppendLine("== Idle: shell plus one blank tab (private window) ==");
            log.AppendLine(CultureInfo.InvariantCulture, $"shell private working set: {Mb(idle.ShellBytes)}   (plain working set {Mb(self.WorkingSet64)}, private bytes {Mb(self.PrivateMemorySize64)})");
            log.AppendLine(CultureInfo.InvariantCulture, $"engine processes (all): {Mb(idle.EngineBytes)}   total: {Mb(idle.TotalBytes)}");
            log.AppendLine(CultureInfo.InvariantCulture, $"budget: shell under 150 MB -> {(idle.ShellBytes < 150L * 1048576 ? "PASS" : "FAIL")}");

            // 2. Twenty real pages, one tab each.
            log.AppendLine();
            log.AppendLine("== 20 tabs ==");
            first.Navigate(PerfSites[0]);
            await WaitForLoadAsync(first);
            for (var i = 1; i < PerfSites.Length; i++)
            {
                var tab = OpenTab(PerfSites[i]);
                await WaitForLoadAsync(tab);
            }
            await Task.Delay(5000);
            log.AppendLine(CultureInfo.InvariantCulture, $"tabs open: {_tabs.Count}");

            var awake = await MemoryMeter.SampleAsync(_tabs);
            LogSample(log, "all tabs awake", awake);
            var attributed = awake.PerTab.Values.Sum();
            log.AppendLine(CultureInfo.InvariantCulture, $"tabs matched to a renderer: {awake.PerTab.Count} of {_tabs.Count}, attributed {Mb(attributed)} of engine {Mb(awake.EngineBytes)}");
            for (var i = 0; i < _tabs.Count; i++)
            {
                log.AppendLine(CultureInfo.InvariantCulture, $"  {i + 1,2} {new Uri(PerfSites[i]).Host,-28} {Mb(awake.BytesFor(_tabs[i])),10}");
            }

            // 3. The idle-timer path: two tabs made to look idle for 11 minutes, setting is 10.
            log.AppendLine();
            log.AppendLine("== Sleeping ==");
            log.AppendLine(CultureInfo.InvariantCulture, $"tab sleep setting: {AppServices.Settings.TabSleepMinutes} minutes");
            var old = TimeProvider.System.GetUtcNow() - TimeSpan.FromMinutes(11);
            _tabs[4].LastActiveAt = old;
            _tabs[5].LastActiveAt = old;
            for (var i = 0; i < _tabs.Count; i++)
            {
                var t = _tabs[i];
                log.AppendLine(CultureInfo.InvariantCulture, $"  pre-check tab {i + 1,2}: engine={t.HasEngine} loading={t.IsLoading} audio={t.IsPlayingAudio} sleeping={t.IsSleeping} pinned={t.IsPinned} active={t == _activeTab} idle={(TimeProvider.System.GetUtcNow() - t.LastActiveAt).TotalMinutes:F1} min");
            }
            var pass = await SleepIdleTabsAsync(ignoreIdle: false);
            for (var i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].LastSleepDiagnostic is { } why)
                {
                    log.AppendLine(CultureInfo.InvariantCulture, $"  tab {i + 1,2}: {why}");
                }
            }
            var sleptNow = _tabs.Where(t => t.IsSleeping).Select(t => _tabs.IndexOf(t) + 1).ToList();
            var expected = sleptNow.Count == 2 && sleptNow.Contains(5) && sleptNow.Contains(6);
            log.AppendLine(CultureInfo.InvariantCulture, $"{(expected ? "PASS" : "FAIL")} timer path slept exactly tabs 5 and 6: slept {string.Join(",", sleptNow)}; saved {Mb(pass.SavedBytes)}");

            // 4. Pinned tab 2 must be skipped by "sleep now".
            TogglePin(_tabs[1]);
            var pinned = _tabs[0];
            log.AppendLine(CultureInfo.InvariantCulture, $"pinned tab moved to the start: {(pinned.IsPinned ? "yes" : "no")} (was tab 2, now at index {_tabs.IndexOf(pinned) + 1})");
            var all = await SleepIdleTabsAsync(ignoreIdle: true);
            log.AppendLine(CultureInfo.InvariantCulture, $"sleep now: slept {all.Slept} tabs, saved {Mb(all.SavedBytes)}");
            var sleepingCount = _tabs.Count(t => t.IsSleeping);
            var suspendedCount = _tabs.Count(t => t.IsSleeping && t.WebView?.CoreWebView2.IsSuspended == true);
            log.AppendLine(CultureInfo.InvariantCulture, $"of the sleeping tabs, fully suspended: {suspendedCount}, memory trimmed only: {sleepingCount - suspendedCount}");
            log.AppendLine(CultureInfo.InvariantCulture, $"{(!pinned.IsSleeping ? "PASS" : "FAIL")} pinned tab stayed awake");
            log.AppendLine(CultureInfo.InvariantCulture, $"{(!_activeTab!.IsSleeping ? "PASS" : "FAIL")} active tab stayed awake");
            log.AppendLine(CultureInfo.InvariantCulture, $"sleeping tabs: {sleepingCount} of {_tabs.Count}");

            await Task.Delay(8000);
            var asleep = await MemoryMeter.SampleAsync(_tabs);
            LogSample(log, "after sleeping (8 s later)", asleep);
            log.AppendLine(CultureInfo.InvariantCulture,
                $"engine memory: {Mb(awake.EngineBytes)} -> {Mb(asleep.EngineBytes)} ({Mb(awake.EngineBytes - asleep.EngineBytes)} less); total {Mb(awake.TotalBytes)} -> {Mb(asleep.TotalBytes)}");
            await Task.Delay(30000);
            var later = await MemoryMeter.SampleAsync(_tabs);
            LogSample(log, "after sleeping (40 s later)", later);
            log.AppendLine(CultureInfo.InvariantCulture,
                $"engine memory: {Mb(awake.EngineBytes)} -> {Mb(later.EngineBytes)} ({Mb(awake.EngineBytes - later.EngineBytes)} less); total {Mb(awake.TotalBytes)} -> {Mb(later.TotalBytes)}");
            log.AppendLine(CultureInfo.InvariantCulture, $"counter 'memory saved today': {Mb(AppServices.MemorySaved.TodayBytes)}");
            log.AppendLine("tab sleeping hover text sample: " + _tabs.First(t => t.IsSleeping).TooltipText.Replace("\r\n", " | ", StringComparison.Ordinal).Replace("\n", " | ", StringComparison.Ordinal));

            // 5. Waking a sleeping tab.
            var target = _tabs.First(t => t.IsSleeping);
            var index = _tabs.IndexOf(target) + 1;
            var watch = Stopwatch.StartNew();
            await ActivateTabAsync(target);
            var answer = await target.WebView!.CoreWebView2.ExecuteScriptAsync("document.readyState");
            watch.Stop();
            log.AppendLine(CultureInfo.InvariantCulture,
                $"{(!target.IsSleeping && !target.WebView.CoreWebView2.IsSuspended ? "PASS" : "FAIL")} tab {index} woke and answered ({answer}) in {watch.ElapsedMilliseconds} ms");
        }
        catch (Exception ex)
        {
            log.AppendLine("Self-test failed: " + ex); // not-ui
        }
        finally
        {
            Directory.CreateDirectory(AppPaths.LogFolder);
            await File.WriteAllTextAsync(Path.Combine(AppPaths.LogFolder, "perf-selftest.txt"), log.ToString());
            Microsoft.UI.Xaml.Application.Current.Exit();
        }
    }

    private static void LogSample(StringBuilder log, string label, MemorySample sample) =>
        log.AppendLine(CultureInfo.InvariantCulture,
            $"{label}: shell {Mb(sample.ShellBytes)}, engine {Mb(sample.EngineBytes)}, total {Mb(sample.TotalBytes)}");

    /// <summary>Waits until the tab's page has loaded (or 30 seconds have passed).</summary>
    private static async Task WaitForLoadAsync(BrowserTab tab)
    {
        var watch = Stopwatch.StartNew();
        var sawLoading = false;
        while (watch.Elapsed < TimeSpan.FromSeconds(30))
        {
            await Task.Delay(300);
            sawLoading |= tab.IsLoading;
            if (tab.WebView?.CoreWebView2 is not null && !tab.IsLoading && (sawLoading || watch.Elapsed > TimeSpan.FromSeconds(4)))
            {
                return;
            }
        }
    }
}
#endif
