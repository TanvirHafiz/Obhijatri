#if DEBUG
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.UI.Xaml;
using Obhijatri.AI.Scoring;
using Obhijatri.App.Browser;
using Obhijatri.App.Services;
using Obhijatri.Core;

namespace Obhijatri.App;

/// <summary>
/// Developer-only "এটা কি প্রতারণা?" self-test (Debug builds, --scamcheck-selftest), in a private
/// window. Part A runs the real page script and scorer on local pages (made-up scam pages and clean
/// ones). Part B runs it on real news, bank and bKash pages. Part C tests layer 2 against a fake
/// Ollama server on this computer: a quick answer, a server error, no server, and a server that is
/// too slow (the request must give up after 20 seconds). Results go to
/// %LOCALAPPDATA%\Obhijatri\logs\scamcheck-selftest.txt, screenshots to logs\scamcheck.
/// </summary>
public sealed partial class MainWindow
{
    private static readonly (string Path, string Expect, string Html)[] ScamCheckPages =
    [
        ("/scam", "red", """
            <!doctype html><meta charset="utf-8"><title>bKash Login</title>
            <body><h1>bKash</h1>
            <p>বিকাশ অ্যাকাউন্ট আপডেট করুন। এখনই আপডেট না করলে অ্যাকাউন্ট ব্লক হয়ে যাবে। আপনার বিকাশ পিন দিন।</p>
            <div id="countdown">00:09:59</div>
            <form action="http://collect.example.net/x"><input name="user"><input type="password" name="pw"><input name="otp" placeholder="OTP"><button>Login</button></form>
            </body>
            """),
        ("/prize", "notgreen", """
            <!doctype html><meta charset="utf-8"><title>লাকি ড্র বিজয়ী</title>
            <body><p>অভিনন্দন! আপনি বিজয়ী হয়েছেন। লাকি ড্র থেকে ৫০,০০০ টাকা পুরস্কার জিতেছেন। পুরস্কার পেতে নম্বর দিন।</p>
            <form><input name="mobile" placeholder="মোবাইল নম্বর"><button>Claim</button></form></body>
            """),
        ("/whatsapp", "notgreen", """
            <!doctype html><meta charset="utf-8"><title>ঘরে বসে আয়</title>
            <body><p>ঘরে বসে আয় করুন, দৈনিক আয় ৩০০০ টাকা। রেজিস্ট্রেশন ফি লাগবে। এখনই যোগাযোগ করুন।</p>
            <a href="https://wa.me/8801712345678">WhatsApp us</a></body>
            """),
        ("/article", "green", """
            <!doctype html><meta charset="utf-8"><title>প্রতারণা থেকে সাবধান</title>
            <body><article><h1>প্রতারণা থেকে সাবধান</h1>
            <p>প্রতারকরা ফোন করে বলে আপনি লটারি জিতেছেন বা সিম বন্ধ হয়ে যাবে। কেউ বলে অ্যাকাউন্ট বন্ধ হয়ে যাবে। বিকাশ ও নগদের কর্মকর্তারা বলেছেন, কাউকে পিন বা ওটিপি দেবেন না।</p>
            <a href="https://wa.me/?text=share">share</a></article></body>
            """),
        ("/shop", "green", """
            <!doctype html><meta charset="utf-8"><title>Daily Groceries</title>
            <body><h1>Daily Groceries</h1><p>Flash sale ends soon. Pay with bKash or Nagad. সীমিত সময়ের অফার।</p>
            <div class="countdown">02:15:33</div><form><input type="search" name="q" placeholder="Search"></form></body>
            """),
        ("/login", "green", """
            <!doctype html><meta charset="utf-8"><title>Sign in</title>
            <body><h1>Sign in</h1><form><input name="user"><input type="password" name="pw"><button>Sign in</button></form></body>
            """),
    ];

    private static readonly string[] ScamCheckRealSites =
    [
        "https://www.prothomalo.com/",
        "https://www.thedailystar.net/",
        "https://www.bkash.com/",
        "https://www.dutchbanglabank.com/",
        "https://www.daraz.com.bd/",
    ];

    private static volatile string _fakeOllamaBehavior = "ok";

    internal async Task RunScamCheckSelfTestAsync()
    {
        var log = new StringBuilder();
        HttpListener? pages = null;
        HttpListener? ollama = null;
        var originalOllama = AppServices.Settings.OllamaEnabled;
        void Save()
        {
            Directory.CreateDirectory(AppPaths.LogFolder);
            File.WriteAllText(System.IO.Path.Combine(AppPaths.LogFolder, "scamcheck-selftest.txt"), log.ToString());
        }

        var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _ = Task.Delay(TimeSpan.FromMinutes(9)).ContinueWith(_ =>
        {
            log.AppendLine("Stopped by the time limit (9 minutes).");
            Directory.CreateDirectory(AppPaths.LogFolder);
            File.WriteAllText(System.IO.Path.Combine(AppPaths.LogFolder, "scamcheck-selftest.txt"), log.ToString());
            queue.TryEnqueue(() => Application.Current.Exit());
        }, TaskScheduler.Default);

        try
        {
            var tab = _activeTab!;
            for (var wait = 0; tab.WebView?.CoreWebView2 is null && wait < 150; wait++)
            {
                await Task.Delay(200);
            }
            var core = tab.WebView!.CoreWebView2;

            pages = new HttpListener();
            pages.Prefixes.Add("http://127.0.0.1:38222/");
            pages.Start();
            _ = Task.Run(() => ServeFixtureLoopAsync(pages));

            // ---- A: local pages through the real script and scorer ----
            log.AppendLine("== A: local pages (made up) ==");
            foreach (var (path, expect, _) in ScamCheckPages)
            {
                _ = await NavigateAndWaitAsync(core, "http://127.0.0.1:38222" + path);
                await Task.Delay(700);
                var watch = Stopwatch.StartNew();
                var (assessment, page) = await AssessTabAsync(tab);
                watch.Stop();

                var ok = expect switch
                {
                    "green" => assessment.Level == ScamLevel.Green,
                    "red" => assessment.Level == ScamLevel.Red,
                    _ => assessment.Level != ScamLevel.Green,
                };
                log.AppendLine(CultureInfo.InvariantCulture,
                    $"{(ok ? "PASS" : "FAIL")} {path,-10} expected {expect,-8} got {assessment.Level,-6} score {assessment.Score,3} in {watch.ElapsedMilliseconds} ms; forms {page.Forms.Count}, countdown {page.HasCountdown}, contact link {page.HasContactLink}; reasons: {string.Join(", ", assessment.TopReasons.Select(r => r.Signal))}");
                await CaptureScamPanelAsync(assessment, path.Trim('/') + ".png");
                Save();
            }

            // ---- B: real sites ----
            log.AppendLine();
            log.AppendLine("== B: real sites (green expected for all of them) ==");
            foreach (var site in ScamCheckRealSites)
            {
                try
                {
                    var (loaded, status) = await NavigateAndWaitAsync(core, site);
                    await Task.Delay(3000);
                    var (assessment, page) = await AssessTabAsync(tab);
                    log.AppendLine(CultureInfo.InvariantCulture,
                        $"{(assessment.Level == ScamLevel.Green ? "PASS" : "CHECK")} {site}: {assessment.Level} score {assessment.Score}; loaded={loaded} ({status}); text {page.Text.Length} chars, forms {page.Forms.Count}; reasons: {string.Join(", ", assessment.Reasons.Select(r => r.Signal + "=" + r.Weight))}");
                }
                catch (Exception ex)
                {
                    log.AppendLine(CultureInfo.InvariantCulture, $"{site}: error {ex.GetType().Name}");
                }
                Save();
            }

            // ---- C: layer 2 against a fake Ollama ----
            log.AppendLine();
            log.AppendLine("== C: layer 2 (Ollama) ==");
            ollama = new HttpListener();
            ollama.Prefixes.Add("http://127.0.0.1:38223/");
            ollama.Start();
            _ = Task.Run(() => FakeOllamaLoopAsync(ollama));

            var sample = ScamScorer.Assess(PageSignals.Parse("https://x.example.top/", "{\"title\":\"t\",\"text\":\"you have won\",\"forms\":[{\"phone\":true}]}"), ShieldFacts.None);
            var samplePage = PageSignals.AddressOnly("https://x.example.top/");

            AppServices.Settings.OllamaEnabled = true;

            Environment.SetEnvironmentVariable("OBHIJATRI_OLLAMA_URL", "http://127.0.0.1:38223/");
            var models = await OllamaService.ListModelsAsync();
            log.AppendLine(Verdict(models is { Count: 2 }, "fake server: model list read: " + string.Join(", ", models ?? [])));

            _fakeOllamaBehavior = "ok";
            var watchOk = Stopwatch.StartNew();
            var answer = await OllamaService.ExplainAsync(sample, samplePage, CancellationToken.None);
            log.AppendLine(Verdict(answer is not null && answer.Contains("প্রতারণা", StringComparison.Ordinal), $"quick answer in {watchOk.ElapsedMilliseconds} ms: {Short(answer)}"));

            _fakeOllamaBehavior = "error";
            log.AppendLine(Verdict(await OllamaService.ExplainAsync(sample, samplePage, CancellationToken.None) is null, "server error gives no explanation (rule result stands)"));

            Environment.SetEnvironmentVariable("OBHIJATRI_OLLAMA_URL", "http://127.0.0.1:38299/"); // nothing listens here
            var watchDown = Stopwatch.StartNew();
            log.AppendLine(Verdict(await OllamaService.ExplainAsync(sample, samplePage, CancellationToken.None) is null && watchDown.Elapsed < TimeSpan.FromSeconds(5),
                $"no server gives no explanation in {watchDown.ElapsedMilliseconds} ms"));
            log.AppendLine(Verdict(await OllamaService.ListModelsAsync() is null, "no server: model list is null"));

            Environment.SetEnvironmentVariable("OBHIJATRI_OLLAMA_URL", "http://127.0.0.1:38223/");
            _fakeOllamaBehavior = "slow";
            var watchSlow = Stopwatch.StartNew();
            var slow = await OllamaService.ExplainAsync(sample, samplePage, CancellationToken.None);
            watchSlow.Stop();
            log.AppendLine(Verdict(slow is null && watchSlow.Elapsed > TimeSpan.FromSeconds(18) && watchSlow.Elapsed < TimeSpan.FromSeconds(24),
                $"slow server (answers after 40 s) gives up after {watchSlow.Elapsed.TotalSeconds:F1} s"));

            AppServices.Settings.OllamaEnabled = false;
            log.AppendLine(Verdict(await OllamaService.ExplainAsync(sample, samplePage, CancellationToken.None) is null, "switched off: nothing is asked"));

            // What is shown when layer 2 is on but there is no answer.
            AppServices.Settings.OllamaEnabled = true;
            await CaptureScamPanelAsync(sample, "with-ai-section.png");
        }
        catch (Exception ex)
        {
            log.AppendLine("Self-test failed: " + ex); // not-ui
        }
        finally
        {
            Environment.SetEnvironmentVariable("OBHIJATRI_OLLAMA_URL", null);
            AppServices.Settings.OllamaEnabled = originalOllama;
            pages?.Stop();
            ollama?.Stop();
            Directory.CreateDirectory(AppPaths.LogFolder);
            await File.WriteAllTextAsync(System.IO.Path.Combine(AppPaths.LogFolder, "scamcheck-selftest.txt"), log.ToString());
            Application.Current.Exit();
        }
    }

    /// <summary>Puts the result panel over the page for a moment and renders the window (a popup would not be captured).</summary>
    private async Task CaptureScamPanelAsync(ScamAssessment assessment, string fileName)
    {
        var panel = BuildScamPanel(assessment, out _);
        var host = new Microsoft.UI.Xaml.Controls.Border
        {
            Child = panel,
            Padding = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SolidBackgroundFillColorBaseBrush"],
        };
        Root.RequestedTheme = ElementTheme.Light;
        ContentHost.Children.Add(host);
        await Task.Delay(500);
        var folder = System.IO.Path.Combine(AppPaths.LogFolder, "scamcheck");
        Directory.CreateDirectory(folder);
        await RenderRootToPngAsync(System.IO.Path.Combine("..", "scamcheck", fileName));
        ContentHost.Children.Remove(host);
    }

    private static async Task ServeFixtureLoopAsync(HttpListener listener)
    {
        try
        {
            while (listener.IsListening)
            {
                var context = await listener.GetContextAsync();
                try
                {
                    var page = ScamCheckPages.FirstOrDefault(p => p.Path == context.Request.Url!.AbsolutePath);
                    if (page.Html is null)
                    {
                        context.Response.StatusCode = 404;
                        context.Response.Close();
                        continue;
                    }
                    var body = Encoding.UTF8.GetBytes(page.Html);
                    context.Response.ContentType = "text/html; charset=utf-8";
                    context.Response.ContentLength64 = body.Length;
                    await context.Response.OutputStream.WriteAsync(body);
                    context.Response.Close();
                }
                catch (Exception ex) when (ex is HttpListenerException or InvalidOperationException or IOException)
                {
                    // The browser dropped this connection; keep serving.
                }
            }
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
        {
            // Stopped.
        }
    }

    /// <summary>Speaks just enough of Ollama's API: /api/tags and /api/generate, with a chosen behaviour.</summary>
    private static async Task FakeOllamaLoopAsync(HttpListener listener)
    {
        try
        {
            while (listener.IsListening)
            {
                var context = await listener.GetContextAsync();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var path = context.Request.Url!.AbsolutePath;
                        string body;
                        var status = 200;
                        if (path == "/api/tags")
                        {
                            body = "{\"models\":[{\"name\":\"llama3.2:latest\"},{\"name\":\"qwen2.5:7b\"}]}";
                        }
                        else if (path == "/api/generate")
                        {
                            switch (_fakeOllamaBehavior)
                            {
                                case "slow":
                                    await Task.Delay(TimeSpan.FromSeconds(40));
                                    body = "{\"response\":\"too late\"}";
                                    break;
                                case "error":
                                    status = 500;
                                    body = "{\"error\":\"model failed\"}";
                                    break;
                                default:
                                    body = "{\"response\":\"**মনে হচ্ছে** এটি প্রতারণা হতে পারে। পুরস্কারের কথা বলে আপনার নম্বর চাওয়া হচ্ছে।\",\"done\":true}";
                                    break;
                            }
                        }
                        else
                        {
                            status = 404;
                            body = string.Empty;
                        }

                        var bytes = Encoding.UTF8.GetBytes(body);
                        context.Response.StatusCode = status;
                        context.Response.ContentType = "application/json";
                        context.Response.ContentLength64 = bytes.Length;
                        await context.Response.OutputStream.WriteAsync(bytes);
                        context.Response.Close();
                    }
                    catch (Exception ex) when (ex is HttpListenerException or InvalidOperationException or IOException or ObjectDisposedException)
                    {
                        // The client gave up (a timeout is what is being tested).
                    }
                });
            }
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
        {
            // Stopped.
        }
    }
}
#endif
