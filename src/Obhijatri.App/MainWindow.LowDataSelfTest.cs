#if DEBUG
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Obhijatri.App.Browser;
using Obhijatri.App.Services;
using Obhijatri.Core;

namespace Obhijatri.App;

/// <summary>
/// Developer-only low data mode self-test (Debug builds, --lowdata-selftest), in a private window.
/// Part A serves a page from localhost with autoplaying audio, lazy images and a YouTube embed and
/// checks each behaviour off and on. Part B loads the five BD news sites with the mode off and on
/// (cache cleared each time) and counts every byte the engine received, including from frames,
/// through the engine's own network events. Results go to
/// %LOCALAPPDATA%\Obhijatri\logs\lowdata-selftest.txt and the app then exits.
/// </summary>
public sealed partial class MainWindow
{
    private const int LowDataRounds = 2;

    private sealed class NetworkTally
    {
        public long Bytes;
        public int Requests;
        public readonly Dictionary<string, string> UrlByRequest = new();
        public readonly Dictionary<string, long> BytesByHost = new(StringComparer.OrdinalIgnoreCase);

        public void Reset()
        {
            Bytes = 0;
            Requests = 0;
            UrlByRequest.Clear();
            BytesByHost.Clear();
        }
    }

    internal async Task RunLowDataSelfTestAsync()
    {
        var log = new StringBuilder();
        var originalMode = AppServices.Settings.LowDataMode;
        HttpListener? listener = null;
        try
        {
            var tab = _activeTab!;
            for (var wait = 0; tab.WebView?.CoreWebView2 is null && wait < 150; wait++)
            {
                await Task.Delay(200);
            }
            var core = tab.WebView!.CoreWebView2;
            var tally = await StartNetworkTallyAsync(core);

            // ---- Part A: a page we control ----
            var prefix = "http://127.0.0.1:38218/";
            listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();
            var imageHits = 0;
            _ = ServeLowDataPageAsync(listener, () => Interlocked.Increment(ref imageHits));

            log.AppendLine("== Part A: local test page ==");
            foreach (var on in new[] { false, true })
            {
                await SetLowDataAsync(on);
                _ = await NavigateAndWaitAsync(core, "about:blank");
                Interlocked.Exchange(ref imageHits, 0);
                tally.Reset();
                var (ok, status) = await NavigateAndWaitAsync(core, prefix + "page");
                await Task.Delay(6000);
                var state = await core.ExecuteScriptAsync(
                    "JSON.stringify({autoPlaying: !document.getElementById('vauto')?.paused, scriptPlaying: !document.getElementById('vscript')?.paused, scriptPlay: window.result.scriptPlay,"
                    + " lazyImages: [...document.images].filter(i => i.loading === 'lazy').length, images: document.images.length})");
                var youtubeBytes = tally.BytesByHost.Where(p => p.Key.Contains("youtube", StringComparison.OrdinalIgnoreCase)).Sum(p => p.Value);
                log.AppendLine(CultureInfo.InvariantCulture,
                    $"low data {(on ? "ON " : "OFF")}: loaded={ok} ({status}); {Unquote(state)}; image requests served={imageHits}; youtube bytes={youtubeBytes}; total bytes={tally.Bytes}");
                if (on)
                {
                    await CapturePageAsync(core, "http://local-lowdata-test/", true, "lowdata");
                }
            }

            if (Environment.GetCommandLineArgs().Contains("--quick", StringComparer.Ordinal))
            {
                return;
            }

            // ---- Part B: real sites ----
            log.AppendLine();
            log.AppendLine("== Part B: five BD news sites, cache cleared before each load ==");
            log.AppendLine("site,low_data,round,load_ms,bytes,requests");
            var rows = new List<(string Site, bool On, double Ms, long Bytes, int Requests)>();
            for (var round = 0; round < LowDataRounds; round++)
            {
                foreach (var site in BenchmarkSites)
                {
                    bool[] modes = round % 2 == 0 ? [false, true] : [true, false];
                    foreach (var on in modes)
                    {
                        await SetLowDataAsync(on);
                        await core.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.DiskCache | CoreWebView2BrowsingDataKinds.CacheStorage);
                        _ = await NavigateAndWaitAsync(core, "about:blank");
                        tally.Reset();
                        var watch = Stopwatch.StartNew();
                        var (ok, status) = await NavigateAndWaitAsync(core, site);
                        watch.Stop();
                        await Task.Delay(5000);
                        var ms = ok ? watch.Elapsed.TotalMilliseconds : double.NaN;
                        if (!ok)
                        {
                            log.AppendLine(CultureInfo.InvariantCulture, $"# load failed: {site} low_data={on} status={status}");
                        }
                        if (round == 0)
                        {
                            await CapturePageAsync(core, site, on, "lowdata");
                        }
                        rows.Add((site, on, ms, tally.Bytes, tally.Requests));
                        log.AppendLine(CultureInfo.InvariantCulture, $"{site},{(on ? "on" : "off")},{round + 1},{ms:F0},{tally.Bytes},{tally.Requests}");
                    }
                }
            }

            log.AppendLine();
            log.AppendLine("Median per site (bytes received, requests, load ms):");
            double offTotal = 0, onTotal = 0;
            foreach (var site in BenchmarkSites)
            {
                var off = rows.Where(r => r.Site == site && !r.On).ToList();
                var on = rows.Where(r => r.Site == site && r.On).ToList();
                var bytesOff = Median(off.Select(r => (double)r.Bytes));
                var bytesOn = Median(on.Select(r => (double)r.Bytes));
                offTotal += bytesOff;
                onTotal += bytesOn;
                log.AppendLine(CultureInfo.InvariantCulture,
                    $"  {site,-32} off {bytesOff / 1048576.0,6:F2} MB  on {bytesOn / 1048576.0,6:F2} MB  change {100 * (bytesOn - bytesOff) / bytesOff,6:F1}%   requests {Median(off.Select(r => (double)r.Requests)):F0} -> {Median(on.Select(r => (double)r.Requests)):F0}   load {Median(off.Select(r => r.Ms)):F0} -> {Median(on.Select(r => r.Ms)):F0} ms");
            }
            log.AppendLine(CultureInfo.InvariantCulture,
                $"  {"all sites",-32} off {offTotal / 1048576.0,6:F2} MB  on {onTotal / 1048576.0,6:F2} MB  change {100 * (onTotal - offTotal) / offTotal,6:F1}%");
        }
        catch (Exception ex)
        {
            log.AppendLine("Self-test failed: " + ex); // not-ui
        }
        finally
        {
            listener?.Stop();
            AppServices.Settings.LowDataMode = originalMode;
            Directory.CreateDirectory(AppPaths.LogFolder);
            await File.WriteAllTextAsync(Path.Combine(AppPaths.LogFolder, "lowdata-selftest.txt"), log.ToString());
            Microsoft.UI.Xaml.Application.Current.Exit();
        }
    }

    private static string Unquote(string json) => json.Trim('"').Replace("\\\"", "\"", StringComparison.Ordinal);

    /// <summary>Switches the setting and waits for the page script to be added or removed.</summary>
    private static async Task SetLowDataAsync(bool on)
    {
        AppServices.Settings.LowDataMode = on;
        await Task.Delay(1000);
    }

    /// <summary>Counts bytes and requests for the page and all its frames (out-of-process frames included).</summary>
    private static async Task<NetworkTally> StartNetworkTallyAsync(CoreWebView2 core)
    {
        var tally = new NetworkTally();

        void OnRequest(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
        {
            using var doc = JsonDocument.Parse(e.ParameterObjectAsJson);
            tally.Requests++;
            var id = doc.RootElement.GetProperty("requestId").GetString();
            var url = doc.RootElement.GetProperty("request").GetProperty("url").GetString();
            if (id is not null && url is not null)
            {
                tally.UrlByRequest[id] = url;
            }
        }

        void OnFinished(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
        {
            using var doc = JsonDocument.Parse(e.ParameterObjectAsJson);
            var length = (long)doc.RootElement.GetProperty("encodedDataLength").GetDouble();
            tally.Bytes += length;
            var id = doc.RootElement.GetProperty("requestId").GetString();
            if (id is not null && tally.UrlByRequest.TryGetValue(id, out var url) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                tally.BytesByHost[uri.Host] = tally.BytesByHost.GetValueOrDefault(uri.Host) + length;
            }
        }

        async void OnAttached(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
        {
            // A frame or worker in its own process: turn networking events on for it, then let it start.
            string? session;
            using (var doc = JsonDocument.Parse(e.ParameterObjectAsJson))
            {
                session = doc.RootElement.GetProperty("sessionId").GetString();
            }
            if (session is null)
            {
                return;
            }
            foreach (var (method, parameters) in new[]
                     {
                         ("Network.enable", "{}"),
                         ("Target.setAutoAttach", "{\"autoAttach\":true,\"waitForDebuggerOnStart\":true,\"flatten\":true}"),
                         ("Runtime.runIfWaitingForDebugger", "{}"),
                     })
            {
                try
                {
                    await core.CallDevToolsProtocolMethodForSessionAsync(session, method, parameters);
                }
                catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or ArgumentException or InvalidOperationException)
                {
                    // Not every kind of target supports every call.
                }
            }
        }

        core.GetDevToolsProtocolEventReceiver("Network.requestWillBeSent").DevToolsProtocolEventReceived += OnRequest;
        core.GetDevToolsProtocolEventReceiver("Network.loadingFinished").DevToolsProtocolEventReceived += OnFinished;
        core.GetDevToolsProtocolEventReceiver("Target.attachedToTarget").DevToolsProtocolEventReceived += OnAttached;
        await core.CallDevToolsProtocolMethodAsync("Network.enable", "{}");
        await core.CallDevToolsProtocolMethodAsync("Target.setAutoAttach", "{\"autoAttach\":true,\"waitForDebuggerOnStart\":true,\"flatten\":true}");
        return tally;
    }

    private static Task ServeLowDataPageAsync(HttpListener listener, Action onImage) => Task.Run(async () =>
    {
        var images = new StringBuilder();
        for (var i = 0; i < 40; i++)
        {
            images.Append(CultureInfo.InvariantCulture, $"<img src=\"/img?i={i}\" width=\"300\" height=\"300\" style=\"display:block\">\n");
        }
        // A muted video made from a canvas (no media file needed): one starts by its autoplay
        // attribute, one by a script calling play(). Both are added by script, after load.
        const string VideoScript = """
            <script>
            window.result = {};
            (async () => {
              const c = document.createElement('canvas'); c.width = 64; c.height = 64;
              const ctx = c.getContext('2d');
              const rec = new MediaRecorder(c.captureStream(10), { mimeType: 'video/webm' });
              const chunks = []; rec.ondataavailable = e => chunks.push(e.data);
              const stopped = new Promise(r => rec.onstop = r);
              rec.start(); let i = 0;
              const t = setInterval(() => { ctx.fillStyle = 'hsl(' + (i++ * 30) + ',80%,50%)'; ctx.fillRect(0, 0, 64, 64); }, 100);
              await new Promise(r => setTimeout(r, 1500)); clearInterval(t); rec.stop(); await stopped;
              const url = URL.createObjectURL(new Blob(chunks, { type: 'video/webm' }));
              const a = document.createElement('video'); a.id = 'vauto'; a.muted = true; a.loop = true; a.autoplay = true; a.src = url; document.body.prepend(a);
              const b = document.createElement('video'); b.id = 'vscript'; b.muted = true; b.loop = true; b.src = url; document.body.prepend(b);
              b.play().then(() => window.result.scriptPlay = 'ok', e => window.result.scriptPlay = e.name);
            })();
            </script>
            """;
        var page = "<!doctype html><meta charset=\"utf-8\"><title>low data test</title>\n"
            + VideoScript + "\n"
            + "<iframe src=\"https://www.youtube.com/embed/aqz-KE-bpKQ\" width=\"400\" height=\"300\"></iframe>\n"
            + images;

        var wav = new byte[44 + 8000];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
        BitConverter.GetBytes(36 + 8000).CopyTo(wav, 4);
        Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(wav, 8);
        BitConverter.GetBytes(16).CopyTo(wav, 16);
        BitConverter.GetBytes((short)1).CopyTo(wav, 20);
        BitConverter.GetBytes((short)1).CopyTo(wav, 22);
        BitConverter.GetBytes(8000).CopyTo(wav, 24);
        BitConverter.GetBytes(8000).CopyTo(wav, 28);
        BitConverter.GetBytes((short)1).CopyTo(wav, 32);
        BitConverter.GetBytes((short)8).CopyTo(wav, 34);
        Encoding.ASCII.GetBytes("data").CopyTo(wav, 36);
        BitConverter.GetBytes(8000).CopyTo(wav, 40);
        Array.Fill(wav, (byte)0x80, 44, 8000);
        var gif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");

        try
        {
            while (listener.IsListening)
            {
                var context = await listener.GetContextAsync();
                var path = context.Request.Url!.AbsolutePath;
                byte[] body;
                if (path == "/page")
                {
                    context.Response.ContentType = "text/html; charset=utf-8";
                    body = Encoding.UTF8.GetBytes(page);
                }
                else if (path == "/silence.wav")
                {
                    context.Response.ContentType = "audio/wav";
                    body = wav;
                }
                else if (path == "/img")
                {
                    onImage();
                    context.Response.ContentType = "image/gif";
                    body = gif;
                }
                else
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    continue;
                }
                context.Response.ContentLength64 = body.Length;
                await context.Response.OutputStream.WriteAsync(body);
                context.Response.Close();
            }
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
        {
            // The listener was stopped; nothing more to serve.
        }
    });
}
#endif
