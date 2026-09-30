using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Obhijatri.AI.Scoring;
using Obhijatri.App.Browser;
using Obhijatri.App.Localization;
using Obhijatri.App.Services;

namespace Obhijatri.App;

/// <summary>
/// "এটা কি প্রতারণা?": the person presses the button and gets a green, yellow or red estimate with
/// the top three reasons in plain Bangla. The rule based check (layer 1) works offline and answers at
/// once. If the local AI (Ollama) is switched on, a short explanation follows (layer 2); if it is
/// missing, slow or fails, the flyout says so and the rule based result stands.
/// </summary>
public sealed partial class MainWindow
{
    private void UpdateScamCheckButton()
    {
        ScamCheckButton.IsEnabled = _activeTab is { HasEngine: true } tab && tab.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase);
        ScamCheckText.Text = Strings.Get("ScamCheckButtonText");
        SetLabel(ScamCheckButton, "ScamCheckTooltip");
    }

    /// <summary>
    /// Collects what the page asks for and says, and judges it together with the scam shield's facts
    /// about the address. While one of our own warning pages is showing, only the address is judged
    /// (the warning's own words would look like a scam).
    /// </summary>
    internal async Task<(ScamAssessment Assessment, PageSignals Page)> AssessTabAsync(BrowserTab tab)
    {
        var url = tab.Url;
        var page = PageSignals.AddressOnly(url);

        if (!tab.ShowsWarningPage && tab.WebView?.CoreWebView2 is { } core)
        {
            try
            {
                var raw = await core.ExecuteScriptAsync(PageBridge.ReadScript("scam-signals.js"));
                page = PageSignals.Parse(url, JsonSerializer.Deserialize<string>(raw));
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException or JsonException)
            {
                // The page could not be read; judge the address alone.
            }
        }

        var facts = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? ScamShieldService.Facts(uri) : ShieldFacts.None;
        return (ScamScorer.Assess(page, facts), page);
    }

    private async void ScamCheckButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTab is not { HasEngine: true } tab)
        {
            return;
        }

        ScamCheckButton.IsEnabled = false;
        try
        {
            var (assessment, page) = await AssessTabAsync(tab);
            ShowScamResult(assessment, page);
        }
        finally
        {
            UpdateScamCheckButton();
        }
    }

    private void ShowScamResult(ScamAssessment assessment, PageSignals page)
    {
        var panel = BuildScamPanel(assessment, out var aiText);
        var flyout = new Flyout { Content = panel, Placement = FlyoutPlacementMode.BottomEdgeAlignedRight };
        var cancellation = new CancellationTokenSource();
        flyout.Closed += (_, _) =>
        {
            cancellation.Cancel();
            cancellation.Dispose();
        };
        flyout.ShowAt(ScamCheckButton);

        if (aiText is not null)
        {
            _ = ShowAiExplanationAsync(aiText, assessment, page, cancellation.Token);
        }
    }

    /// <summary>The result as it is shown: the coloured verdict, the top reasons, the AI part if switched on, and the estimate note.</summary>
    private static StackPanel BuildScamPanel(ScamAssessment assessment, out TextBlock? aiText)
    {
        var (bannerStyle, titleStyle, titleKey) = assessment.Level switch
        {
            ScamLevel.Red => ("ScamRedBannerStyle", "ScamRedTitleStyle", "ScamLevelRed"),
            ScamLevel.Yellow => ("ScamYellowBannerStyle", "ScamYellowTitleStyle", "ScamLevelYellow"),
            _ => ("ScamGreenBannerStyle", "ScamGreenTitleStyle", "ScamLevelGreen"),
        };

        var panel = new StackPanel { Width = 380, Spacing = 10 };
        panel.Children.Add(new Border
        {
            Style = AppStyle(bannerStyle),
            Child = new TextBlock { Text = Strings.Get(titleKey), Style = AppStyle(titleStyle) },
        });

        panel.Children.Add(new TextBlock { Text = Strings.Get("ScamReasonsTitle"), Style = AppStyle("BodyStrongTextBlockStyle") });
        var top = assessment.TopReasons;
        if (top.Count == 0)
        {
            panel.Children.Add(Wrapped(Strings.Get("ScamReasonNone")));
        }
        foreach (var reason in top)
        {
            panel.Children.Add(Wrapped("• " + ReasonText(reason))); // not-ui
        }

        aiText = null;
        if (AppServices.Settings.OllamaEnabled)
        {
            panel.Children.Add(new TextBlock { Text = Strings.Get("ScamAiTitle"), Style = AppStyle("BodyStrongTextBlockStyle"), TextWrapping = TextWrapping.Wrap });
            aiText = Wrapped(Strings.Get("ScamAiWorking"));
            panel.Children.Add(aiText);
        }

        panel.Children.Add(new TextBlock
        {
            Text = Strings.Get("ScamEstimateNote"),
            TextWrapping = TextWrapping.Wrap,
            Style = AppStyle("SecondaryCaptionTextBlockStyle"),
        });
        return panel;
    }

    private static async Task ShowAiExplanationAsync(TextBlock target, ScamAssessment assessment, PageSignals page, CancellationToken cancellation)
    {
        var answer = await OllamaService.ExplainAsync(assessment, page, cancellation);
        if (cancellation.IsCancellationRequested)
        {
            return;
        }
        target.Text = answer ?? Strings.Get("ScamAiUnavailable");
    }

    private static string ReasonText(ScamReason reason)
    {
        var brand = reason.BrandNameKey is { } key ? Strings.Get(key) : string.Empty;
        return reason.Signal switch
        {
            ScamSignal.LookalikeDomain => Strings.Format("ScamReasonLookalikeFormat", brand, reason.RealDomain ?? string.Empty),
            ScamSignal.ImpersonatesBrand => Strings.Format("ScamReasonImpersonatesBrandFormat", brand),
            ScamSignal.BrandOnWrongDomain => Strings.Format("ScamReasonBrandOnWrongDomainFormat", brand),
            ScamSignal.OfficialSite => Strings.Format("ScamReasonOfficialSiteFormat", brand),
            _ => Strings.Get("ScamReason" + reason.Signal),
        };
    }

    private static TextBlock Wrapped(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };

    private static Style AppStyle(string key) => (Style)Application.Current.Resources[key];
}
