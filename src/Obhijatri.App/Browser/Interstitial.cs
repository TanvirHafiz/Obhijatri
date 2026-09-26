using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Obhijatri.App.Localization;
using Obhijatri.App.Services;
using Obhijatri.Safety.ScamShield;

namespace Obhijatri.App.Browser;

public enum InterstitialAction
{
    Back,
    Proceed,
}

/// <summary>Which warning is shown, so "continue anyway" is remembered under the right kind of choice.</summary>
public enum InterstitialKind
{
    NoHttps,
    Scam,
}

/// <summary>
/// Full-page local warnings (a site without HTTPS now; scam warnings in Milestone 6). The page is
/// shown with NavigateToString and has no script. Its two links point at the reserved host
/// obhijatri.invalid, which the tab intercepts and never loads. Each warning has a random nonce in
/// its links, so another page cannot trigger "continue" by navigating to such a link itself.
/// </summary>
internal sealed class Interstitial
{
    public const string ActionHost = "obhijatri.invalid";
    private static readonly Lazy<string> Template = new(ReadTemplate);

    private Interstitial(string nonce, string targetUrl, string host, InterstitialKind kind)
    {
        Nonce = nonce;
        TargetUrl = targetUrl;
        Host = host;
        Kind = kind;
    }

    public string Nonce { get; }

    public InterstitialKind Kind { get; }

    /// <summary>The address the user wanted (shown in the address bar while the warning is up).</summary>
    public string TargetUrl { get; }

    public string Host { get; }

    public string Html { get; private init; } = string.Empty;

    public string Title { get; private init; } = string.Empty;

    /// <summary>The warning for a site that could not be opened over HTTPS.</summary>
    public static Interstitial NoHttps(string httpUrl, string host)
    {
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var title = Strings.Get("HttpsWarningTitle");
        var html = Fill(new Dictionary<string, string>
        {
            ["LANG"] = AppServices.Settings.IsBangla ? "bn" : "en",
            ["TITLE"] = title,
            ["ACCENT"] = "#c50f1f",
            ["HEADING"] = Strings.Get("HttpsWarningHeading"),
            ["BODY"] = Strings.Get("HttpsWarningBody"),
            ["DETAIL"] = httpUrl,
            ["ADVICE"] = Strings.Get("HttpsWarningAdvice"),
            ["BACK"] = Strings.Get("InterstitialBack"),
            ["PROCEED"] = Strings.Get("HttpsWarningProceed"),
        }, nonce);
        return new Interstitial(nonce, httpUrl, host, InterstitialKind.NoHttps) { Html = html, Title = title };
    }

    /// <summary>The scam shield warning: a lookalike domain, a known scam site, or a Safe Browsing hit.</summary>
    public static Interstitial Scam(string url, string host, ScamVerdict verdict)
    {
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var title = Strings.Get("ScamWarningTitle");
        var (heading, body) = verdict.Reason switch
        {
            ScamReasonKind.Lookalike => (
                Strings.Format("ScamWarningHeadingLookalikeFormat", Strings.Get(verdict.BrandNameKey!)),
                Strings.Format("ScamWarningBodyLookalikeFormat", verdict.RealDomain!)),
            ScamReasonKind.KnownScam => (Strings.Get("ScamWarningHeadingKnownScam"), Strings.Get("ScamWarningBodyKnownScam")),
            _ => (Strings.Get("ScamWarningHeadingSafeBrowsing"), Strings.Get("ScamWarningBodySafeBrowsing")),
        };
        var html = Fill(new Dictionary<string, string>
        {
            ["LANG"] = AppServices.Settings.IsBangla ? "bn" : "en",
            ["TITLE"] = title,
            ["ACCENT"] = "#c50f1f",
            ["HEADING"] = heading,
            ["BODY"] = body,
            ["DETAIL"] = host,
            ["ADVICE"] = Strings.Get("ScamWarningAdvice"),
            ["BACK"] = Strings.Get("InterstitialBack"),
            ["PROCEED"] = Strings.Get("ScamWarningProceed"),
        }, nonce);
        return new Interstitial(nonce, url, host, InterstitialKind.Scam) { Html = html, Title = title };
    }

    /// <summary>Recognises a click on one of the warning's links. Returns null for anything else.</summary>
    public InterstitialAction? ParseAction(Uri uri)
    {
        if (!string.Equals(uri.Host, ActionHost, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        var query = uri.Query.TrimStart('?');
        var expected = "n=" + Nonce;
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(query), Encoding.ASCII.GetBytes(expected)))
        {
            return null;
        }
        return uri.AbsolutePath switch
        {
            "/back" => InterstitialAction.Back,
            "/proceed" => InterstitialAction.Proceed,
            _ => null,
        };
    }

    public static bool IsActionUrl(Uri uri) => string.Equals(uri.Host, ActionHost, StringComparison.OrdinalIgnoreCase);

    private static string Fill(Dictionary<string, string> values, string nonce)
    {
        var html = Template.Value
            .Replace("{{BACK_URL}}", $"https://{ActionHost}/back?n={nonce}", StringComparison.Ordinal)
            .Replace("{{PROCEED_URL}}", $"https://{ActionHost}/proceed?n={nonce}", StringComparison.Ordinal);
        foreach (var (key, value) in values)
        {
            // Every value is HTML-encoded: the address in particular comes from the web.
            html = html.Replace("{{" + key + "}}", WebUtility.HtmlEncode(value), StringComparison.Ordinal);
        }
        return html;
    }

    private static string ReadTemplate()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Obhijatri.App.Web.interstitial.html")
                           ?? throw new InvalidOperationException("Missing interstitial template");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
