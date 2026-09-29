#if DEBUG
using System.Globalization;
using System.Net;
using System.Text;
using Obhijatri.App.Browser;
using Obhijatri.Core;

namespace Obhijatri.App;

/// <summary>
/// Developer-only download scanning self-test (Debug builds, --downloads-selftest), in a private
/// window. Serves a small file from localhost (with a real attachment header, no network needed) and
/// checks it clears Obhijatri.Safety.Downloads.AttachmentScanner (Mark of the Web applied) and a
/// second file with a double extension is blocked. Results go to
/// %LOCALAPPDATA%\Obhijatri\logs\downloads-selftest.txt and the app then exits.
/// </summary>
public sealed partial class MainWindow
{
    internal async Task RunDownloadsSelfTestAsync()
    {
        var log = new StringBuilder();
        HttpListener? listener = null;
        try
        {
            var tab = _activeTab!;
            for (var wait = 0; tab.WebView?.CoreWebView2 is null && wait < 150; wait++)
            {
                await Task.Delay(200);
            }
            await Task.Delay(500);

            var prefix = "http://127.0.0.1:38217/";
            listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();
            var files = new Dictionary<string, string>
            {
                ["ordinary.txt"] = "hello from the self-test, not a virus",
                ["invoice.pdf.exe"] = "same bytes, but a double extension",
            };
            _ = ServeAsync(listener, files);

            var ordinary = await DownloadOneAsync(tab, prefix + "download?name=ordinary.txt");
            log.AppendLine(CultureInfo.InvariantCulture,
                $"{(ordinary is { IsCompleted: true, IsBlocked: false } ? "PASS" : "FAIL")} ordinary.txt: completed={ordinary?.IsCompleted}, blocked={ordinary?.IsBlocked}");
            if (ordinary is not null)
            {
                var motw = ordinary.FilePath + ":Zone.Identifier";
                log.AppendLine(CultureInfo.InvariantCulture, $"{(File.Exists(motw) ? "PASS" : "FAIL")} Mark of the Web written: {File.Exists(motw)}");
                TryDelete(ordinary.FilePath);
            }

            var doubleExt = await DownloadOneAsync(tab, prefix + "download?name=invoice.pdf.exe");
            var effectivelyBlocked = doubleExt is { IsBlocked: true } or { IsCompleted: false, IsInProgress: false, IsScanning: false };
            log.AppendLine(CultureInfo.InvariantCulture,
                $"{(effectivelyBlocked ? "PASS" : "FAIL")} invoice.pdf.exe (double extension): blocked={doubleExt?.IsBlocked}, statusText={doubleExt?.StatusText}, fileName={doubleExt?.FileName}, path={doubleExt?.FilePath}, exists={(doubleExt is null ? "?" : File.Exists(doubleExt.FilePath))}, completed={doubleExt?.IsCompleted}, scanning={doubleExt?.IsScanning}, inProgress={doubleExt?.IsInProgress}");
            if (doubleExt is not null)
            {
                TryDelete(doubleExt.FilePath);
                TryDelete(doubleExt.FilePath + ":Zone.Identifier");
            }
        }
        catch (Exception ex)
        {
            log.AppendLine("Self-test failed: " + ex); // not-ui
        }
        finally
        {
            listener?.Stop();
            Directory.CreateDirectory(AppPaths.LogFolder);
            await File.WriteAllTextAsync(Path.Combine(AppPaths.LogFolder, "downloads-selftest.txt"), log.ToString());
            Microsoft.UI.Xaml.Application.Current.Exit();
        }
    }

    private async Task<Downloads.DownloadItem?> DownloadOneAsync(BrowserTab tab, string url)
    {
        var before = _downloads.Count;
        tab.Navigate(url);
        for (var i = 0; i < 200 && _downloads.Count == before; i++)
        {
            await Task.Delay(150);
        }
        if (_downloads.Count == before)
        {
            return null;
        }
        var item = _downloads[0];
        for (var i = 0; i < 300 && (item.IsInProgress || item.IsScanning); i++)
        {
            await Task.Delay(200);
        }
        return item;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) { File.Delete(path); }
        }
        catch (IOException)
        {
        }
    }

    private static Task ServeAsync(HttpListener listener, IReadOnlyDictionary<string, string> files) => Task.Run(async () =>
    {
        try
        {
            while (listener.IsListening)
            {
                var context = await listener.GetContextAsync();
                var name = context.Request.QueryString["name"];
                if (name is null || !files.TryGetValue(name, out var body))
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    continue;
                }

                var bytes = Encoding.UTF8.GetBytes(body);
                context.Response.ContentType = "application/octet-stream";
                context.Response.AddHeader("Content-Disposition", $"attachment; filename=\"{name}\"");
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes);
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
