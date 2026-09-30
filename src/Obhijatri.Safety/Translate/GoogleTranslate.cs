using System.Net;
using Obhijatri.Safety.PaymentLock;

namespace Obhijatri.Safety.Translate;

/// <summary>Why a page is not offered to Google Translate.</summary>
public enum TranslateRefusal
{
    None,
    NotWeb,
    PrivateWindow,
    PaymentSite,
    LocalOrPrivateAddress,
    UnsupportedAddress,
    AlreadyTranslated,
    TooLong,
}

/// <summary>
/// The "translate with Google" route: the tab is sent to Google's translation of the same page
/// (translate.goog). Google fetches the page itself, so what Google learns is the page address,
/// and only what Google can fetch without signing in is translated. It is off unless the person
/// switches it on, and never offered for private windows, banking and payment sites, or addresses
/// that are only reachable from this computer or network.
/// </summary>
public static class GoogleTranslate
{
    public const string ProxyDomain = "translate.goog";
    public const string TargetLanguage = "bn";
    public const int MaxAddressLength = 1800;

    /// <summary>True for a page that is already a Google translation.</summary>
    public static bool IsTranslatedHost(string host) =>
        host.EndsWith("." + ProxyDomain, StringComparison.OrdinalIgnoreCase)
        || host.Equals("translate.google.com", StringComparison.OrdinalIgnoreCase);

    /// <summary>Why this page may not be translated this way, or <see cref="TranslateRefusal.None"/>.</summary>
    public static TranslateRefusal Check(Uri uri, bool isPrivateWindow)
    {
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
        {
            return TranslateRefusal.NotWeb;
        }
        if (IsTranslatedHost(uri.Host))
        {
            return TranslateRefusal.AlreadyTranslated;
        }
        if (isPrivateWindow)
        {
            return TranslateRefusal.PrivateWindow;
        }
        if (HttpsUpgrade.IsLocalOrPrivate(uri.Host))
        {
            return TranslateRefusal.LocalOrPrivateAddress;
        }
        if (IPAddress.TryParse(uri.Host.Trim('[', ']'), out _) || !uri.IsDefaultPort)
        {
            return TranslateRefusal.UnsupportedAddress;
        }
        if (PaymentSites.IsPaymentSite(uri.Host))
        {
            return TranslateRefusal.PaymentSite;
        }
        if (uri.AbsoluteUri.Length > MaxAddressLength)
        {
            return TranslateRefusal.TooLong;
        }
        return TranslateRefusal.None;
    }

    /// <summary>The address of Google's Bangla translation of <paramref name="uri"/>, or null if <see cref="Check"/> refuses it.</summary>
    public static Uri? BuildUrl(Uri uri, bool isPrivateWindow)
    {
        if (Check(uri, isPrivateWindow) != TranslateRefusal.None)
        {
            return null;
        }

        if (uri.Scheme == Uri.UriSchemeHttp)
        {
            // translate.goog names carry https sites; an http page goes through the classic address.
            return new Uri("https://translate.google.com/translate?sl=auto&tl=" + TargetLanguage + "&hl=" + TargetLanguage
                           + "&u=" + Uri.EscapeDataString(uri.AbsoluteUri));
        }

        var query = uri.Query.Length > 1 ? uri.Query + "&" : "?";
        return new Uri("https://" + EncodeHost(uri.IdnHost) + "." + ProxyDomain + uri.AbsolutePath + query
                       + "_x_tr_sl=auto&_x_tr_tl=" + TargetLanguage + "&_x_tr_hl=" + TargetLanguage + "&_x_tr_pto=wapp" + uri.Fragment);
    }

    /// <summary>www.my-site.com becomes www-my--site-com: a dot is a hyphen and a hyphen is doubled.</summary>
    public static string EncodeHost(string host) =>
        host.Replace("-", "--", StringComparison.Ordinal).Replace('.', '-');

    /// <summary>
    /// The site behind a translate.goog address (www-my--site-com.translate.goog is www.my-site.com), or
    /// null if the host is not one. Lets the scam shield judge the real site, not Google's wrapper.
    /// </summary>
    public static string? OriginalHost(string host)
    {
        const string suffix = "." + ProxyDomain;
        if (!host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) || host.Length == suffix.Length)
        {
            return null;
        }

        var encoded = host[..^suffix.Length].ToLowerInvariant();
        return encoded.Replace("--", "\u0001", StringComparison.Ordinal).Replace('-', '.').Replace('\u0001', '-');
    }
}
