using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Obhijatri.App.Browser;
using Obhijatri.App.Localization;
using Obhijatri.App.Services;
using Obhijatri.Safety.Filtering;

namespace Obhijatri.App;

/// <summary>
/// The shield button: how many ads and trackers were blocked on this page, whether the connection is
/// secure, and the per-site "show ads on this site" choice.
/// </summary>
public sealed partial class MainWindow
{
    // "Continue anyway" for sites without HTTPS, or past a scam shield warning, lasts for this run
    // only and is never saved.
    private static readonly HashSet<string> SessionHttpAllowed = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _privateHttpAllowed = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> SessionScamAllowed = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _privateScamAllowed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _privateAdsAllowed = new(StringComparer.OrdinalIgnoreCase);

    public bool GetAdsAllowed(string host) =>
        IsPrivate ? _privateAdsAllowed.GetValueOrDefault(host) : AppServices.SitePreferences.GetAdsAllowed(host);

    private void SetAdsAllowed(string host, bool allowed)
    {
        if (IsPrivate)
        {
            _privateAdsAllowed[host] = allowed;
        }
        else
        {
            AppServices.SitePreferences.SetAdsAllowed(host, allowed);
        }
    }

    public bool IsHttpAllowed(string host) => (IsPrivate ? _privateHttpAllowed : SessionHttpAllowed).Contains(host);

    public void AllowHttp(string host) => (IsPrivate ? _privateHttpAllowed : SessionHttpAllowed).Add(host);

    public bool IsScamAllowed(string host) => (IsPrivate ? _privateScamAllowed : SessionScamAllowed).Contains(host);

    public void AllowScamSite(string host) => (IsPrivate ? _privateScamAllowed : SessionScamAllowed).Add(host);

    private void UpdateShieldButton()
    {
        var tab = _activeTab;
        var count = tab?.BlockedCount ?? 0;
        ShieldCountText.Text = Formatting.Number(count);
        // No count on sites whose ads cannot be blocked: a number there would read as "ads removed".
        var cannotBlock = UnblockableAds.NameFor(tab?.SiteHost) is not null;
        ShieldCountText.Visibility = count > 0 && FilterService.Enabled && !cannotBlock ? Visibility.Visible : Visibility.Collapsed;
        ShieldIcon.Glyph = tab is { Kind: TabKind.Web } && !tab.IsSecure && tab.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            ? "" // warning: not a secure connection
            : ""; // shield
        ShieldButton.IsEnabled = tab is { Kind: TabKind.Web } && tab.SiteHost is not null;
        SetLabel(ShieldButton, "ShieldTooltip");

        var paymentLock = tab is { Kind: TabKind.Web, IsPaymentLockActive: true };
        PaymentLockBadge.Visibility = paymentLock ? Visibility.Visible : Visibility.Collapsed;
        if (paymentLock)
        {
            PaymentLockText.Text = Strings.Get("PaymentLockBadge");
            SetLabel(PaymentLockBadge, "PaymentLockTooltip");
        }
    }

    private void ShieldButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTab is not { Kind: TabKind.Web, SiteHost: { } host } tab)
        {
            return;
        }

        var panel = new StackPanel { Width = 320, Spacing = 12 };

        panel.Children.Add(new TextBlock
        {
            Text = tab.IsSecure ? Strings.Get("ShieldSecure") : Strings.Get("ShieldNotSecure"),
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources[tab.IsSecure ? "SuccessCaptionTextBlockStyle" : "CriticalCaptionTextBlockStyle"],
        });

        panel.Children.Add(new TextBlock
        {
            Text = !FilterService.Enabled
                ? Strings.Get("ShieldBlockingOff")
                : UnblockableAds.NameFor(host) is { } siteName
                    ? Strings.Format("ShieldCannotBlockFormat", siteName)
                    : Strings.Format("ShieldBlockedFormat", Formatting.Number(tab.BlockedCount)),
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
        });

        var allow = new ToggleSwitch
        {
            Header = Strings.Format("ShieldAllowAdsFormat", host),
            IsOn = GetAdsAllowed(host),
            OnContent = Strings.Get("ToggleOn"),
            OffContent = Strings.Get("ToggleOff"),
            IsEnabled = FilterService.Enabled,
        };
        allow.Toggled += (_, _) =>
        {
            SetAdsAllowed(host, allow.IsOn);
            tab.Reload();
        };
        panel.Children.Add(allow);

        panel.Children.Add(new TextBlock
        {
            Text = Strings.Get("ShieldAllowAdsHint"),
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["SecondaryCaptionTextBlockStyle"],
        });

        new Flyout { Content = panel, Placement = FlyoutPlacementMode.BottomEdgeAlignedRight }.ShowAt(ShieldButton);
    }
}
