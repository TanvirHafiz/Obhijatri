#if DEBUG
using System.Globalization;
using System.Text;
using Obhijatri.App.Browser;
using Obhijatri.App.Services;
using Obhijatri.Core;

namespace Obhijatri.App;

/// <summary>
/// Developer-only scam shield self-test (Debug builds, --scamshield-selftest), in a private window.
/// Every address here is either a fixture-style made-up domain or a real, harmless site; nothing
/// resolves over the network before the warning is decided, so this needs no internet connection.
/// Results go to %LOCALAPPDATA%\Obhijatri\logs\scamshield-selftest.txt and the app then exits.
/// </summary>
public sealed partial class MainWindow
{
    internal async Task RunScamShieldSelfTestAsync()
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
                ("https://example.com/", false),
                ("https://bkash-verify.xyz/", true),
                ("https://faceb00k.com/", true),
                ("https://pay.bkash.com/", false),
            ];

            foreach (var (url, expectWarning) in cases)
            {
                tab.Navigate(url);
                await SettleAsync(tab);
                var warning = tab.DebugWarningNonce is not null;
                log.AppendLine(CultureInfo.InvariantCulture,
                    $"{(warning == expectWarning ? "PASS" : "FAIL")} {url}: warning={warning}, address bar={tab.Url}");
            }

            // "Continue anyway", then no second warning for the same site this session.
            tab.Navigate("https://bkash-verify.xyz/");
            await SettleAsync(tab);
            if (tab.DebugWarningNonce is { } nonce)
            {
                await CaptureAsync(tab, "scam-warning.png");

                tab.WebView!.CoreWebView2.Navigate($"https://{Interstitial.ActionHost}/proceed?n=0000");
                await SettleAsync(tab);
                log.AppendLine(CultureInfo.InvariantCulture,
                    $"{(tab.DebugWarningNonce == nonce ? "PASS" : "FAIL")} wrong code ignored: still on warning={tab.DebugWarningNonce == nonce}");

                tab.WebView.CoreWebView2.Navigate($"https://{Interstitial.ActionHost}/proceed?n={nonce}");
                await SettleAsync(tab);
                log.AppendLine(CultureInfo.InvariantCulture,
                    $"{(tab.DebugWarningNonce is null ? "PASS" : "FAIL")} continue anyway: address bar={tab.Url}");
                await CaptureAsync(tab, "scam-continued.png");

                tab.Navigate("https://bkash-verify.xyz/");
                await SettleAsync(tab);
                log.AppendLine(CultureInfo.InvariantCulture,
                    $"{(tab.DebugWarningNonce is null ? "PASS" : "FAIL")} second visit in same session: warning={tab.DebugWarningNonce is not null}");
            }
            else
            {
                log.AppendLine("FAIL bkash-verify.xyz did not warn on the second attempt");
            }

            // Turning the setting off skips the check entirely.
            AppServices.Settings.ScamShieldEnabled = false;
            tab.Navigate("https://nagad-bd.xyz/");
            await SettleAsync(tab);
            log.AppendLine(CultureInfo.InvariantCulture,
                $"{(tab.DebugWarningNonce is null ? "PASS" : "FAIL")} setting off: warning={tab.DebugWarningNonce is not null}");
            AppServices.Settings.ScamShieldEnabled = true;
        }
        catch (Exception ex)
        {
            log.AppendLine("Self-test failed: " + ex); // not-ui
        }
        finally
        {
            Directory.CreateDirectory(AppPaths.LogFolder);
            await File.WriteAllTextAsync(Path.Combine(AppPaths.LogFolder, "scamshield-selftest.txt"), log.ToString());
            Microsoft.UI.Xaml.Application.Current.Exit();
        }
    }
}
#endif
