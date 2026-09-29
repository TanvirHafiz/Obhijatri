#if DEBUG
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Obhijatri.App.Browser;
using Obhijatri.App.Services;
using Obhijatri.Core;
using Obhijatri.Core.Reader;
using Windows.Media.SpeechSynthesis;

namespace Obhijatri.App;

/// <summary>
/// Developer-only reader mode, font fix and voice self-test (Debug builds, --reader-selftest), in a
/// private window. Part A: a local article page with navigation, ads, comments and a footer around
/// it, checking only the story is extracted. Part B: the Bangla font fix on a local page. Part C: the
/// Windows voices. Part D: the front stories of five real BD news sites. Results go to
/// %LOCALAPPDATA%\Obhijatri\logs\reader-selftest.txt (screenshots in logs\reader) and the app exits.
/// </summary>
public sealed partial class MainWindow
{
    private const string ArticleFixture = """
        <!doctype html><meta charset="utf-8"><title>স্থানীয় পরীক্ষা</title>
        <body>
        <header><h1>সাইটের নাম</h1><nav><a href="/a">হোম</a> <a href="/b">খেলা</a> <a href="/c">বিনোদন</a></nav></header>
        <div class="sidebar"><p>এটি সাইডবারের একটি বিজ্ঞাপন যা দীর্ঘ লেখা হলেও প্রবন্ধের অংশ নয় এবং বাদ যাওয়া উচিত।</p></div>
        <article>
          <h1>ঢাকায় নতুন সেতু চালু হচ্ছে</h1>
          <p>রাজধানীর যানজট কমাতে নতুন সেতুটি আগামী সপ্তাহে যান চলাচলের জন্য খুলে দেওয়া হবে বলে জানিয়েছেন কর্তৃপক্ষ। প্রকল্পটি তিন বছরে শেষ হয়েছে।</p>
          <p>সেতুর দুই পাশে পথচারীদের জন্য আলাদা পথ রাখা হয়েছে। রাতে আলো জ্বলবে সৌরবিদ্যুতে, ফলে বিদ্যুৎ খরচ কম হবে।</p>
          <h2>যা জানা দরকার</h2>
          <ul><li>উদ্বোধনের দিন সকাল দশটায় শুরু হবে অনুষ্ঠান</li></ul>
          <p>বিস্তারিত পরে জানানো হবে। এখানে আরও একটি লম্বা অনুচ্ছেদ আছে যাতে প্রবন্ধটি স্পষ্টভাবে মূল লেখা হিসেবে চিনে নেওয়া যায়।</p>
          <div class="related-posts"><p>সম্পর্কিত খবর: এটি আরেকটি দীর্ঘ লাইন যা সম্পর্কিত খবরের অংশ এবং বাদ যাওয়া উচিত বলে ধরা হয়।</p></div>
        </article>
        <div id="comments"><p>একজন পাঠকের মন্তব্য যা যথেষ্ট লম্বা এবং প্রবন্ধের অংশ নয় তাই এটি বাদ যাবে বলে আশা করা হচ্ছে।</p></div>
        <footer><p>সর্বস্বত্ব সংরক্ষিত। এই ফুটারের লেখাটিও বেশ লম্বা এবং প্রবন্ধের অংশ নয় তাই বাদ যাওয়া উচিত।</p></footer>
        </body>
        """;

    private const string FontFixture = """
        <!doctype html><meta charset="utf-8"><title>font test</title>
        <body style="font-family: Arial, sans-serif; font-size: 28px">
        <p id="bn">আমি বাংলায় গান গাই, ক্ষুদ্র শিক্ষা বিজ্ঞান স্বাধীনতা</p>
        <p id="en">Plain English text stays as it is</p>
        <p id="mix">Mixed: বাংলা and English</p>
        <div id="late"></div>
        <script>setTimeout(() => { document.getElementById('late').textContent = 'পরে যোগ হওয়া লেখা'; }, 500);</script>
        </body>
        """;

    private const string BijoyFixture = """
        <!doctype html><meta charset="utf-8"><title>bijoy test</title>
        <body style="font-family: Arial; font-size: 26px">
        <p id="bijoy" style="font-family: SutonnyMJ, Arial">Avwg evsjvq Mvb MvB, cÖ_g Av‡jv</p>
        <p id="reph" style="font-family: 'SutonnyOMJ'">Kg© gvP© KvwZ©K</p>
        <p id="plain">Plain English and আমি বাংলায় লিখি</p>
        </body>
        """;

    internal async Task RunReaderSelfTestAsync()
    {
        var log = new StringBuilder();
        HttpListener? listener = null;
        var originalFonts = AppServices.Settings.FixBanglaFonts;

        // A page that never finishes must not leave the test running for ever: write what there is and stop.
        var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _ = Task.Delay(TimeSpan.FromMinutes(7)).ContinueWith(_ =>
        {
            log.AppendLine("Stopped by the time limit (7 minutes).");
            Directory.CreateDirectory(AppPaths.LogFolder);
            File.WriteAllText(System.IO.Path.Combine(AppPaths.LogFolder, "reader-selftest.txt"), log.ToString());
            queue.TryEnqueue(() => Microsoft.UI.Xaml.Application.Current.Exit());
        }, TaskScheduler.Default);

        try
        {
            var tab = _activeTab!;
            for (var wait = 0; tab.WebView?.CoreWebView2 is null && wait < 150; wait++)
            {
                await Task.Delay(200);
            }
            var core = tab.WebView!.CoreWebView2;

            listener = new HttpListener();
            listener.Prefixes.Add("http://127.0.0.1:38220/");
            listener.Start();
            _ = Task.Run(async () =>
            {
                try
                {
                    while (listener.IsListening)
                    {
                        var context = await listener.GetContextAsync();
                        try
                        {
                            var path = context.Request.Url!.AbsolutePath;
                            if (path is not ("/font" or "/article" or "/bijoy"))
                            {
                                context.Response.StatusCode = 404; // favicon and other side requests
                                context.Response.Close();
                                continue;
                            }
                            var body = Encoding.UTF8.GetBytes(path == "/font" ? FontFixture : path == "/bijoy" ? BijoyFixture : ArticleFixture);
                            context.Response.ContentType = "text/html; charset=utf-8";
                            context.Response.ContentLength64 = body.Length;
                            await context.Response.OutputStream.WriteAsync(body);
                            context.Response.Close();
                        }
                        catch (Exception ex) when (ex is HttpListenerException or InvalidOperationException or IOException)
                        {
                            // The browser dropped this connection; keep serving the next one.
                        }
                    }
                }
                catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
                {
                }
            });

            var sitesOnly = Environment.GetCommandLineArgs().Contains("--sites-only", StringComparer.Ordinal);
            // ---- A: extraction on a page with junk around the story ----
            if (!sitesOnly)
            {
            log.AppendLine("== A: local article page ==");
            var (loaded, loadStatus) = await NavigateAndWaitAsync(core, "http://127.0.0.1:38220/article");
            log.AppendLine(CultureInfo.InvariantCulture, $"navigation: ok={loaded}, status={loadStatus}, source={core.Source}");
            await Task.Delay(800);
            var article = await ExtractAsync(core);
            if (article is null)
            {
                log.AppendLine("FAIL no article extracted; raw result: " + Short(await core.ExecuteScriptAsync(PageBridge.ReadScript("reader-extract.js"))));
            }
            else
            {
                var all = string.Join("\n", article.Blocks.Select(b => b.Text));
                log.AppendLine(CultureInfo.InvariantCulture, $"title: {article.Title}; blocks: {article.Blocks.Count}");
                foreach (var block in article.Blocks)
                {
                    log.AppendLine(CultureInfo.InvariantCulture, $"  {block.Kind,-9} {Short(block.Text)}");
                }
                log.AppendLine(Verdict(!all.Contains("সাইডবারের", StringComparison.Ordinal), "sidebar left out"));
                log.AppendLine(Verdict(!all.Contains("সম্পর্কিত খবর", StringComparison.Ordinal), "related posts left out"));
                log.AppendLine(Verdict(!all.Contains("মন্তব্য", StringComparison.Ordinal), "comments left out"));
                log.AppendLine(Verdict(!all.Contains("সর্বস্বত্ব", StringComparison.Ordinal), "footer left out"));
                log.AppendLine(Verdict(!all.Contains("হোম", StringComparison.Ordinal), "navigation left out"));
                log.AppendLine(Verdict(article.Blocks.Count(b => b.Kind == ReaderBlockKind.Paragraph) == 3, "three story paragraphs kept"));
                log.AppendLine(Verdict(all.Contains("উদ্বোধনের দিন", StringComparison.Ordinal), "list item kept"));
            }
            await OpenReaderAsync();
            log.AppendLine(Verdict(_readerView is not null, "reader view opened"));
            await Task.Delay(800);
            await CaptureReaderAsync("local-article.png");
            CloseReader();
            log.AppendLine(Verdict(_readerView is null, "reader view closed"));

            // ---- B: the Bangla font fix ----
            log.AppendLine();
            log.AppendLine("== B: Fix Bangla fonts ==");
            foreach (var on in new[] { false, true })
            {
                AppServices.Settings.FixBanglaFonts = on;
                await Task.Delay(1000);
                _ = await NavigateAndWaitAsync(core, "http://127.0.0.1:38220/font");
                await Task.Delay(1500);
                var state = Unquote(await core.ExecuteScriptAsync(
                    "JSON.stringify({bn: getComputedStyle(document.getElementById('bn')).fontFamily, en: getComputedStyle(document.getElementById('en')).fontFamily,"
                    + " mix: getComputedStyle(document.getElementById('mix')).fontFamily, late: getComputedStyle(document.getElementById('late')).fontFamily,"
                    + " faceOk: document.fonts.check('16px ObhijatriBangla', 'ক')})"));
                log.AppendLine(CultureInfo.InvariantCulture, $"fix {(on ? "ON " : "OFF")}: {state}");
                await CapturePageAsync(core, "http://local-fonts/", on, "reader");
            }

            // ---- B2: Bijoy detection and conversion ----
            log.AppendLine();
            log.AppendLine("== B2: Bijoy (SutonnyMJ) text ==");
            _ = await NavigateAndWaitAsync(core, "http://127.0.0.1:38220/bijoy");
            for (var wait = 0; !tab.HasBijoyText && wait < 40; wait++)
            {
                await Task.Delay(250);
            }
            log.AppendLine(Verdict(tab.HasBijoyText, "Bijoy font text detected on the page (chip would show)"));
            var converted = await tab.ConvertBijoyAsync();
            log.AppendLine(Verdict(converted == 2, $"converted {converted} pieces of text (expected 2)"));
            var after = Unquote(await core.ExecuteScriptAsync(
                "JSON.stringify({b: document.getElementById('bijoy').textContent, r: document.getElementById('reph').textContent,"
                + " p: document.getElementById('plain').textContent, f: getComputedStyle(document.getElementById('bijoy')).fontFamily})"));
            log.AppendLine("after: " + after);
            var bijoyText = JsonSerializer.Deserialize<JsonElement>(Regex.Unescape(after));
            log.AppendLine(Verdict(bijoyText.GetProperty("b").GetString()!.Normalize(NormalizationForm.FormC) == "আমি বাংলায় গান গাই, প্রথম আলো".Normalize(NormalizationForm.FormC), "first paragraph converted"));
            log.AppendLine(Verdict(bijoyText.GetProperty("r").GetString()!.Normalize(NormalizationForm.FormC) == "কর্ম মার্চ কার্তিক".Normalize(NormalizationForm.FormC), "reph words converted"));
            log.AppendLine(Verdict(bijoyText.GetProperty("p").GetString() == "Plain English and আমি বাংলায় লিখি", "ordinary text left alone"));
            log.AppendLine(Verdict(!tab.HasBijoyText, "chip hidden after converting"));
            await CapturePageAsync(core, "http://local-bijoy/", true, "reader");

            // ---- C: voices ----
            log.AppendLine();
            log.AppendLine("== C: Windows voices ==");
            foreach (var voice in SpeechSynthesizer.AllVoices)
            {
                log.AppendLine(CultureInfo.InvariantCulture, $"  {voice.Language,-8} {voice.DisplayName}");
            }
            var bangla = SpeechReader.FindBanglaVoice();
            log.AppendLine(bangla is null ? "no Bangla voice installed (reader shows the install note)" : "Bangla voice: " + bangla.DisplayName);
            if (bangla is not null)
            {
                using var synthesizer = new SpeechSynthesizer { Voice = bangla };
                using var stream = await synthesizer.SynthesizeTextToStreamAsync("আমি বাংলায় গান গাই।");
                log.AppendLine(Verdict(stream.Size > 1000, $"synthesised {stream.Size} bytes of {stream.ContentType}"));
            }

            }

            if (Environment.GetCommandLineArgs().Contains("--quick", StringComparer.Ordinal))
            {
                return;
            }

            // ---- D: real news sites ----
            log.AppendLine();
            log.AppendLine("== D: real BD news sites (front story of each) ==");
            var shots = 0;
            foreach (var site in BenchmarkSites)
            {
                try
                {
                    _ = await NavigateAndWaitAsync(core, site);
                    await Task.Delay(3000);
                    var link = Unquote(await core.ExecuteScriptAsync(
                        "(() => { const host = location.hostname; let best = null, len = 0;"
                        + " const skip = /^\\/(tag|topic|category|poll|video|videos|gallery|photo|search|author|live|media|media-en|image|images|epaper)\\b/;"
                        + " for (const a of document.querySelectorAll('a')) {"
                        + "  const parts = a.pathname.split('/').filter(Boolean);"
                        + "  if (a.hostname !== host || parts.length < 2 || skip.test(a.pathname)) continue;"
                        + "  if (!(/\\d{4,}/.test(a.pathname) || parts.length >= 3)) continue;"
                        + "  const n = a.textContent.trim().length; if (n > len && n < 200) { len = n; best = a.href; } }"
                        + " return JSON.stringify(best); })()"));
                    if (link is "null" or "")
                    {
                        log.AppendLine(CultureInfo.InvariantCulture, $"{site}: no story link found");
                        continue;
                    }
                    link = link.Trim('"');
                    _ = await NavigateAndWaitAsync(core, link);
                    await Task.Delay(3000);
                    var extracted = await ExtractAsync(core);
                    if (extracted is null)
                    {
                        log.AppendLine(CultureInfo.InvariantCulture, $"{site}: FAIL nothing extracted from {link}");
                        continue;
                    }
                    var paragraphs = extracted.Blocks.Where(b => b.Kind == ReaderBlockKind.Paragraph).ToList();
                    log.AppendLine(CultureInfo.InvariantCulture,
                        $"{site}: {link}\n   title: {Short(extracted.Title)}\n   blocks {extracted.Blocks.Count}, paragraphs {paragraphs.Count}, characters {extracted.Blocks.Sum(b => b.Text.Length)}\n   first: {Short(paragraphs.FirstOrDefault()?.Text)}\n   last: {Short(extracted.Blocks.Last().Text)}");
                    if (shots++ < 3)
                    {
                        await OpenReaderAsync();
                        await Task.Delay(800);
                        await CaptureReaderAsync(new Uri(site).Host.Replace("www.", string.Empty, StringComparison.Ordinal) + ".png");
                        CloseReader();
                    }
                }
                catch (Exception ex)
                {
                    log.AppendLine(CultureInfo.InvariantCulture, $"{site}: error {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            log.AppendLine("Self-test failed: " + ex); // not-ui
        }
        finally
        {
            listener?.Stop();
            AppServices.Settings.FixBanglaFonts = originalFonts;
            Directory.CreateDirectory(AppPaths.LogFolder);
            await File.WriteAllTextAsync(System.IO.Path.Combine(AppPaths.LogFolder, "reader-selftest.txt"), log.ToString());
            Microsoft.UI.Xaml.Application.Current.Exit();
        }
    }

    private static string Verdict(bool ok, string what) => (ok ? "PASS " : "FAIL ") + what;

    private static string Short(string? text) => text is null ? "(none)" : text.Length <= 90 ? text : text[..90] + "...";

    private static async Task<ReaderArticle?> ExtractAsync(Microsoft.Web.WebView2.Core.CoreWebView2 core)
    {
        var raw = await core.ExecuteScriptAsync(PageBridge.ReadScript("reader-extract.js"));
        return ReaderArticle.Parse(JsonSerializer.Deserialize<string>(raw));
    }

    /// <summary>The reader is native, so it is captured by rendering the window (light theme, see the new tab self-test).</summary>
    private async Task CaptureReaderAsync(string fileName)
    {
        Root.RequestedTheme = Microsoft.UI.Xaml.ElementTheme.Light;
        await Task.Delay(400);
        var folder = System.IO.Path.Combine(AppPaths.LogFolder, "reader");
        Directory.CreateDirectory(folder);
        await RenderRootToPngAsync(System.IO.Path.Combine("..", "reader", fileName));
    }
}
#endif
