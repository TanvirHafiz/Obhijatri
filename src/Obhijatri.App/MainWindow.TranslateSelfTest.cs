#if DEBUG
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.UI.Xaml;
using Obhijatri.App.Localization;
using Obhijatri.App.Services;
using Obhijatri.App.Views;
using Obhijatri.Bangla;
using Obhijatri.Core;
using Obhijatri.Core.Reader;
using Obhijatri.Safety.Translate;

namespace Obhijatri.App;

/// <summary>
/// Developer-only translation self-test (Debug builds, --translate-selftest), in a private window.
/// Part A: the Google route on a real English page (needs internet): the translated address loads, is
/// mostly Bangla, and the scam shield does not warn about Google's wrapper. Part B: the local AI route in
/// the reader against a fake Ollama: paragraphs already in Bangla are left alone, the others are replaced,
/// the original can be shown again, and if Ollama is down it gives up early with a message. Results go to
/// %LOCALAPPDATA%\Obhijatri\logs\translate-selftest.txt.
/// </summary>
public sealed partial class MainWindow
{
    internal async Task RunTranslateSelfTestAsync()
    {
        var log = new StringBuilder();
        HttpListener? ollama = null;
        var originalOllama = AppServices.Settings.OllamaEnabled;
        void Save()
        {
            Directory.CreateDirectory(AppPaths.LogFolder);
            File.WriteAllText(System.IO.Path.Combine(AppPaths.LogFolder, "translate-selftest.txt"), log.ToString());
        }

        var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _ = Task.Delay(TimeSpan.FromMinutes(4)).ContinueWith(_ =>
        {
            log.AppendLine("Stopped by the time limit (4 minutes).");
            Save();
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

            // ---- A: Google route ----
            log.AppendLine("== A: Google Translate route (real network) ==");
            var english = new Uri("https://www.thedailystar.net/");
            log.AppendLine(Verdict(GoogleTranslate.Check(english, isPrivateWindow: true) == TranslateRefusal.PrivateWindow, "refused in a private window (this test window is private)"));
            var target = GoogleTranslate.BuildUrl(english, isPrivateWindow: false)!;
            log.AppendLine("address: " + target);
            var (loaded, status) = await NavigateAndWaitAsync(core, target.AbsoluteUri);
            log.AppendLine(CultureInfo.InvariantCulture, $"navigation: ok={loaded}, status={status}, source={core.Source}, tab url={tab.Url}");
            await Task.Delay(5000);

            var source = core.Source;
            log.AppendLine(Verdict(new Uri(source).Host.EndsWith(".translate.goog", StringComparison.Ordinal), "the tab is on Google's wrapper address: " + new Uri(source).Host));
            log.AppendLine(Verdict(!tab.ShowsWarningPage, "the scam shield did not warn about the wrapper address"));
            var sample = System.Text.Json.JsonSerializer.Deserialize<string>(await core.ExecuteScriptAsync("document.body ? document.body.innerText.slice(0, 3000) : ''")) ?? string.Empty;
            log.AppendLine(Verdict(sample.Any(BanglaText.IsBanglaChar) && BanglaText.IsMostlyBangla(sample, 0.3),
                $"page text is Bangla now: {sample.Count(BanglaText.IsBanglaChar)} Bangla letters of {sample.Count(char.IsLetter)}; start: {Short(sample.Replace('\n', ' '))}"));
            Save();

            // ---- B: local AI route in the reader ----
            log.AppendLine();
            log.AppendLine("== B: local AI in the reader (fake Ollama) ==");
            ollama = new HttpListener();
            ollama.Prefixes.Add("http://127.0.0.1:38223/");
            ollama.Start();
            _ = Task.Run(() => FakeOllamaLoopAsync(ollama));
            _fakeOllamaBehavior = "ok";
            AppServices.Settings.OllamaEnabled = true;
            Environment.SetEnvironmentVariable("OBHIJATRI_OLLAMA_URL", "http://127.0.0.1:38223/");

            var article = new ReaderArticle("An English title", "example.com",
            [
                new ReaderBlock(ReaderBlockKind.Heading, "A heading in English"),
                new ReaderBlock(ReaderBlockKind.Paragraph, "This is the first English paragraph of the story."),
                new ReaderBlock(ReaderBlockKind.Paragraph, "এটি আগে থেকেই বাংলা অনুচ্ছেদ।"),
                new ReaderBlock(ReaderBlockKind.Paragraph, "This is the second English paragraph of the story."),
                new ReaderBlock(ReaderBlockKind.ListItem, "A list item in English"),
            ]);

            var view = new ReaderView(article, () => { }, OllamaService.TranslateAsync);
            ContentHost.Children.Add(view);
            foreach (var child in ContentHost.Children)
            {
                child.Visibility = child == view ? Visibility.Visible : Visibility.Collapsed;
            }
            var before = view.DebugBlockTexts;

            var watch = Stopwatch.StartNew();
            view.StartTranslation();
            for (var wait = 0; (wait == 0 || view.DebugIsTranslating) && wait < 100; wait++)
            {
                await Task.Delay(200);
            }
            var during = view.DebugBlockTexts;
            log.AppendLine(CultureInfo.InvariantCulture, $"translated in {watch.ElapsedMilliseconds} ms; status: {view.DebugStatus}");
            for (var i = 0; i < during.Count; i++)
            {
                log.AppendLine(CultureInfo.InvariantCulture, $"  block {i}: [{Short(before[i])}] -> [{Short(during[i])}]");
            }
            log.AppendLine(Verdict(during[0] != before[0] && during[1] != before[1] && during[2] != before[2] && during[4] != before[4] && during[5].StartsWith("• ", StringComparison.Ordinal) && during[5] != before[5],
                "English title, heading, paragraphs and the list item were replaced (the list bullet kept)"));
            log.AppendLine(Verdict(during[3] == before[3], "the paragraph already in Bangla was left alone"));
            log.AppendLine(Verdict(during[2].Contains("প্রতারণা", StringComparison.Ordinal) && !during[2].Contains('*', StringComparison.Ordinal), "translation shown as plain text (markdown stripped): " + Short(during[2])));
            RequestRoot(ElementTheme.Light);
            await Task.Delay(400);
            Directory.CreateDirectory(System.IO.Path.Combine(AppPaths.LogFolder, "translate"));
            await RenderRootToPngAsync(System.IO.Path.Combine("..", "translate", "reader-translated.png"));

            view.DebugToggleTranslation();
            await Task.Delay(300);
            log.AppendLine(Verdict(view.DebugBlockTexts.SequenceEqual(before), "\"show original\" brings the original text back"));
            view.DebugToggleTranslation();
            await Task.Delay(300);
            log.AppendLine(Verdict(view.DebugBlockTexts.SequenceEqual(during), "\"show translation\" brings it forward again"));
            view.Dispose();
            ContentHost.Children.Remove(view);
            Save();

            // Ollama not running: it should give up after two failures, not wait for every paragraph.
            Environment.SetEnvironmentVariable("OBHIJATRI_OLLAMA_URL", "http://127.0.0.1:38299/");
            var down = new ReaderView(article, () => { }, OllamaService.TranslateAsync);
            ContentHost.Children.Add(down);
            var downWatch = Stopwatch.StartNew();
            down.StartTranslation();
            for (var wait = 0; (wait == 0 || down.DebugIsTranslating) && wait < 150; wait++)
            {
                await Task.Delay(200);
            }
            log.AppendLine(Verdict(down.DebugStatus == Strings.Get("ReaderTranslateFailed") && downWatch.Elapsed < TimeSpan.FromSeconds(20)
                                   && down.DebugBlockTexts.SequenceEqual(before),
                $"Ollama down: gave up after {downWatch.Elapsed.TotalSeconds:F1} s with the message, text unchanged"));
            down.Dispose();
            ContentHost.Children.Remove(down);
        }
        catch (Exception ex)
        {
            log.AppendLine("Self-test failed: " + ex); // not-ui
        }
        finally
        {
            Environment.SetEnvironmentVariable("OBHIJATRI_OLLAMA_URL", null);
            AppServices.Settings.OllamaEnabled = originalOllama;
            ollama?.Stop();
            Save();
            Application.Current.Exit();
        }
    }

    private void RequestRoot(ElementTheme theme) => Root.RequestedTheme = theme;
}
#endif
