#if DEBUG
using System.Globalization;
using System.Text;
using Obhijatri.App.Browser;
using Obhijatri.Core;

namespace Obhijatri.App;

/// <summary>
/// Developer-only HTTPS-only self-test (Debug builds, --https-selftest), in a private window.
/// Results go to %LOCALAPPDATA%\Obhijatri\logs\https-selftest.txt and the app then exits.
/// </summary>
public sealed partial class MainWindow
{
    internal async Task RunHttpsSelfTestAsync()
    {
        var log = new StringBuilder();
        try
        {
            var tab = _activeTab!;
            for (var wait = 0; tab.WebView?.CoreWebView2 is null && wait < 150; wait++)
            {
                await Task.Delay(200);
            }
            await SettleAsync(tab);

            // (address typed, expect warning)
            (string Url, bool ExpectWarning)[] cases =
            [
                ("http://example.com/", false),
                ("http://http.badssl.com/", true),
                ("http://expired.badssl.com/", true),
                ("http://neverssl.com/", true),
            ];

            foreach (var (url, expectWarning) in cases)
            {
                tab.Navigate(url);
                await SettleAsync(tab);
                var warning = tab.DebugWarningNonce is not null;
                log.AppendLine(CultureInfo.InvariantCulture,
                    $"{(warning == expectWarning ? "PASS" : "FAIL")} {url}: warning={warning}, address bar={tab.Url}, secure={tab.IsSecure}");

                if (warning && url.Contains("http.badssl", StringComparison.Ordinal))
                {
                    await CaptureAsync(tab, "https-warning.png");
                    var nonce = tab.DebugWarningNonce!;
                    // The warning is about the address that failed (it can differ from the one typed
                    // if a site redirects).
                    var target = new Uri(tab.DebugWarningTarget!);
                    log.AppendLine(CultureInfo.InvariantCulture, $"     warning is about {target}");

                    // A link with the wrong code (as another page could create) must do nothing.
                    tab.WebView!.CoreWebView2.Navigate($"https://{Interstitial.ActionHost}/proceed?n=0000");
                    await SettleAsync(tab);
                    log.AppendLine(CultureInfo.InvariantCulture,
                        $"{(tab.DebugWarningNonce == nonce ? "PASS" : "FAIL")} wrong code ignored: still on warning={tab.DebugWarningNonce == nonce}");

                    // The real "continue anyway" link.
                    tab.WebView.CoreWebView2.Navigate($"https://{Interstitial.ActionHost}/proceed?n={nonce}");
                    await SettleAsync(tab);
                    var onHttp = tab.DebugWarningNonce is null && Uri.TryCreate(tab.Url, UriKind.Absolute, out var now)
                                 && now.Scheme == Uri.UriSchemeHttp && now.Host == target.Host;
                    log.AppendLine(CultureInfo.InvariantCulture, $"{(onHttp ? "PASS" : "FAIL")} continue anyway: address bar={tab.Url}, secure={tab.IsSecure}");
                    await CaptureAsync(tab, "https-continued.png");

                    // Remembered for this session: no second warning.
                    tab.Navigate(target.AbsoluteUri);
                    await SettleAsync(tab);
                    log.AppendLine(CultureInfo.InvariantCulture,
                        $"{(tab.DebugWarningNonce is null ? "PASS" : "FAIL")} second visit in same session: warning={tab.DebugWarningNonce is not null}, address bar={tab.Url}");
                }
            }
        }
        catch (Exception ex)
        {
            log.AppendLine("Self-test failed: " + ex); // not-ui
        }
        finally
        {
            Directory.CreateDirectory(AppPaths.LogFolder);
            await File.WriteAllTextAsync(Path.Combine(AppPaths.LogFolder, "https-selftest.txt"), log.ToString());
            Microsoft.UI.Xaml.Application.Current.Exit();
        }
    }

    /// <summary>Waits until the tab has stopped loading for a moment (navigations can chain).</summary>
    private static async Task SettleAsync(BrowserTab tab)
    {
        var quiet = 0;
        for (var i = 0; i < 150 && quiet < 8; i++)
        {
            await Task.Delay(200);
            quiet = tab.IsLoading ? 0 : quiet + 1;
        }
    }

    private static async Task CaptureAsync(BrowserTab tab, string name)
    {
        var folder = Path.Combine(AppPaths.LogFolder, "benchmark");
        Directory.CreateDirectory(folder);
        await using var file = File.Create(Path.Combine(folder, name));
        await tab.WebView!.CoreWebView2.CapturePreviewAsync(
            Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, file.AsRandomAccessStream());
    }
}
#endif
