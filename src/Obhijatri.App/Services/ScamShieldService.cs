using Microsoft.UI.Dispatching;
using Obhijatri.Core;
using Obhijatri.Core.Settings;
using Obhijatri.Safety.ScamShield;
using Obhijatri.Safety.ScamShield.SafeBrowsing;

namespace Obhijatri.App.Services;

/// <summary>
/// Owns the scam shield's two data-backed checks: the signed BD scam list (updated once a day) and
/// the optional Google Safe Browsing hash-prefix cache (skipped entirely without an API key). The
/// lookalike detector needs no data and is called directly from <see cref="Check"/>.
/// </summary>
internal static class ScamShieldService
{
    // A bundled, self-signed placeholder key: see Assets/ScamShield/bd-scam-domains.json and
    // PROGRESS.md for how the real list should be signed and hosted (an open decision, plan.md
    // section 9).
    private const string PublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEiPxE3KhnZ7Rpn1YxhcJhYNEZP5gv
        ZDIYufhD5UsCcnAfaA7Qr7G5aCvAOCdKKD/BJw23U7bKPMH5kv9sfwi33A==
        -----END PUBLIC KEY-----
        """;

    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    private static DispatcherQueueTimer? _timer;
    private static HttpClient? _http;
    private static bool _updating;

    public static ScamListStore ScamList { get; } = new(
        Path.Combine(AppPaths.ScamShieldFolder, "bd-scam-domains.json"),
        Path.Combine(AppContext.BaseDirectory, "Assets", "ScamShield", "bd-scam-domains.json"),
        PublicKeyPem);

    public static SafeBrowsingClient SafeBrowsing { get; } = new(
        Path.Combine(AppPaths.ScamShieldFolder, "safebrowsing-prefixes.txt"));

    /// <summary>Cached copy of the setting, so the per-navigation check never touches the database.</summary>
    public static bool Enabled { get; private set; }

    public static void Start(DispatcherQueue ui)
    {
        Enabled = AppServices.Settings.ScamShieldEnabled;
        AppServices.Settings.Changed += (_, key) =>
        {
            if (key == BrowserSettings.Keys.ScamShieldEnabled)
            {
                Enabled = AppServices.Settings.ScamShieldEnabled;
            }
        };

        _timer = ui.CreateTimer();
        _timer.Interval = FirstCheckDelay;
        _timer.Tick += async (timer, _) =>
        {
            timer.Interval = CheckInterval;
            await UpdateNowAsync();
        };
        _timer.Start();
    }

    /// <summary>
    /// A page shown through Google Translate (www-facebook-com.translate.goog) is judged by the site it
    /// really is (www.facebook.com), not by the wrapper name, which would look like a lookalike.
    /// </summary>
    private static Uri Unwrap(Uri uri) =>
        Obhijatri.Safety.Translate.GoogleTranslate.OriginalHost(uri.Host) is { } original
        && Uri.TryCreate(Uri.UriSchemeHttps + "://" + original + uri.AbsolutePath, UriKind.Absolute, out var real)
            ? real
            : uri;

    /// <summary>Fast, offline checks only (no network): under 20 ms per navigation (see tests).</summary>
    public static ScamVerdict? Check(Uri uri)
    {
        uri = Unwrap(uri);
        if (LookalikeDetector.Check(uri) is { } lookalike)
        {
            return lookalike;
        }
        if (ScamList.IsListed(uri.Host))
        {
            return ScamVerdict.KnownScam;
        }
        if (SafeBrowsing.PrefixCount > 0 && SafeBrowsing.IsFlagged(uri))
        {
            return ScamVerdict.SafeBrowsing;
        }
        return null;
    }

    /// <summary>
    /// What the shield knows about an address, from local data only, whatever the shield setting is:
    /// the person asked, so the answer should not depend on the automatic warnings being on.
    /// </summary>
    public static Obhijatri.AI.Scoring.ShieldFacts Facts(Uri uri)
    {
        uri = Unwrap(uri);
        var lookalike = LookalikeDetector.Check(uri);
        return new Obhijatri.AI.Scoring.ShieldFacts(
            ScamList.IsListed(uri.Host),
            SafeBrowsing.PrefixCount > 0 && SafeBrowsing.IsFlagged(uri),
            lookalike?.BrandNameKey,
            lookalike?.RealDomain);
    }

    /// <summary>Downloads the scam list (if a URL is configured) and the Safe Browsing prefixes (if a key is configured).</summary>
    public static async Task UpdateNowAsync()
    {
        if (_updating)
        {
            return;
        }
        _updating = true;
        try
        {
            Directory.CreateDirectory(AppPaths.ScamShieldFolder);
            _http ??= CreateHttpClient();
            using var cancel = new CancellationTokenSource(TimeSpan.FromMinutes(2));

            if (ScamList.IsUpdateDue())
            {
                await ScamList.UpdateAsync(_http, AppServices.Settings.ScamListUrl, cancel.Token);
            }

            var apiKey = ReadSafeBrowsingKey();
            if (apiKey.Length > 0)
            {
                await SafeBrowsing.UpdateAsync(_http, apiKey, cancel.Token);
            }
        }
        finally
        {
            _updating = false;
        }
    }

    private static string ReadSafeBrowsingKey()
    {
        try
        {
            return File.Exists(AppPaths.SafeBrowsingKeyFile) ? File.ReadAllText(AppPaths.SafeBrowsingKeyFile).Trim() : string.Empty;
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(1) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Obhijatri-ScamShield/1.0");
        return http;
    }
}
