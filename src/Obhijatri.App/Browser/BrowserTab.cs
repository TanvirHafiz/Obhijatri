using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Obhijatri.App.Localization;
using Obhijatri.App.Services;
using Obhijatri.Bangla.Phonetic;
using Obhijatri.Core;
using Obhijatri.Core.Settings;
using Obhijatri.Core.Storage;
using Obhijatri.Safety;
using Obhijatri.Safety.Filtering;
using Obhijatri.Safety.ScamShield;

namespace Obhijatri.App.Browser;

public enum TabKind
{
    Web,
    History,
    Settings,
}

/// <summary>What a tab needs from the window that owns it.</summary>
public interface ITabHost
{
    bool IsPrivate { get; }

    /// <summary>Where visits are recorded. Null in private windows.</summary>
    HistoryStore? History { get; }

    /// <summary>Opens a new tab for a popup and returns it once its engine is ready (not navigated).</summary>
    Task<BrowserTab?> OpenPopupTabAsync();

    void OnDownloadStarting(CoreWebView2DownloadStartingEventArgs args);

    /// <summary>Whether Bangla phonetic typing is on for a site (host name).</summary>
    bool GetSitePhonetic(string host);

    /// <summary>The user switched Bangla phonetic typing on or off for a site.</summary>
    void SetSitePhonetic(string host, bool enabled);

    /// <summary>Whether the user chose to see ads on a site.</summary>
    bool GetAdsAllowed(string host);

    /// <summary>The user chose "continue anyway" for a site without HTTPS (this session only).</summary>
    bool IsHttpAllowed(string host);

    void AllowHttp(string host);

    /// <summary>The user chose "continue anyway" on a scam shield warning for this site (this session only).</summary>
    bool IsScamAllowed(string host);

    void AllowScamSite(string host);
}

/// <summary>
/// One tab. A web tab creates its WebView2 only when first shown, so restored sessions
/// start fast and use little memory.
/// </summary>
public sealed partial class BrowserTab : ObservableBase
{
    private readonly ITabHost _host;
    private string _title;
    private string _url;
    private bool _isLoading;
    private bool _canGoBack;
    private bool _canGoForward;
    private IconSource _icon;
    private ImageSource? _favicon;
    private string? _pendingUrl;
    private string? _lastRecordedUrl;
    private bool _lastRecordedTitleMissing;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _sameDocumentRecordTimer;
    private bool _isCreating;
    private readonly PageBridge _bridge = new();
    private CoreWebView2Environment? _environment;
    private int _blockedCount;
    private string _pageHost = string.Empty;
    private string? _mainDocumentUrl;
    private bool _filteringOffForPage;
    private PendingUpgrade? _upgrade;
    private Interstitial? _interstitial;
    private bool _loadingInterstitial;

    /// <summary>An http:// address we switched to https://, waiting to see whether HTTPS works.</summary>
    private sealed record PendingUpgrade(string HttpUrl, string Host, int Hops)
    {
        public ulong? NavigationId { get; set; }
    }
    private readonly List<CoreWebView2Frame> _frames = [];

    public BrowserTab(ITabHost host, TabKind kind, string? url, string? title)
    {
        _host = host;
        Kind = kind;
        _url = kind switch
        {
            TabKind.History => InternalPages.History,
            TabKind.Settings => InternalPages.Settings,
            _ => url ?? string.Empty,
        };
        _pendingUrl = kind == TabKind.Web ? url : null;
        // Built-in pages always use their name in the current language, not a saved title.
        _title = kind != TabKind.Web || string.IsNullOrWhiteSpace(title) ? DefaultTitle() : title;
        _icon = DefaultIcon();
    }

    public TabKind Kind { get; }

    /// <summary>Raised when the page's engine process fails.</summary>
    public event EventHandler? Crashed;

    /// <summary>Raised when the user presses a browser shortcut while the page has focus.</summary>
    public event EventHandler<BrowserShortcut>? ShortcutPressed;

    public string Title { get => _title; private set => Set(ref _title, value); }
    public string Url { get => _url; private set => Set(ref _url, value); }
    public bool IsLoading { get => _isLoading; private set => Set(ref _isLoading, value); }
    public bool CanGoBack { get => _canGoBack; private set => Set(ref _canGoBack, value); }
    public bool CanGoForward { get => _canGoForward; private set => Set(ref _canGoForward, value); }
    public IconSource Icon { get => _icon; private set => Set(ref _icon, value); }

    /// <summary>Ads and trackers blocked on the current page.</summary>
    public int BlockedCount { get => _blockedCount; private set => Set(ref _blockedCount, value); }

    /// <summary>True when the page is served over HTTPS (a secure connection).</summary>
    public bool IsSecure => Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && _interstitial is null;

    /// <summary>The site of the current page, for per-site choices.</summary>
    public string? SiteHost => CurrentHost;

    /// <summary>The site icon as an image, for places that draw it directly (vertical tabs).</summary>
    public ImageSource? Favicon { get => _favicon; private set { if (Set(ref _favicon, value)) { Raise(nameof(HasFavicon)); Raise(nameof(HasNoFavicon)); } } }
    public bool HasFavicon => _favicon is not null;
    public bool HasNoFavicon => _favicon is null;

    /// <summary>Icon glyph used when there is no site icon.</summary>
    public string Glyph => Kind switch
    {
        TabKind.History => "\uE81C",
        TabKind.Settings => "\uE713",
        _ => "\uE774",
    };

    /// <summary>The element shown when the tab is active: a WebView2 or a built-in page.</summary>
    public FrameworkElement? Content { get; private set; }

    public WebView2? WebView { get; private set; }

    public bool IsCreated => Content is not null;

    /// <summary>Used for built-in pages such as History.</summary>
    public void SetContent(FrameworkElement content) => Content = content;

    /// <summary>
    /// Creates the WebView2 inside <paramref name="container"/>. When <paramref name="navigate"/> is
    /// false the pending address is not loaded (popups get their content from the opener).
    /// </summary>
    public async Task<bool> CreateWebViewAsync(Panel container, bool navigate = true)
    {
        if (IsCreated || _isCreating)
        {
            return IsCreated;
        }
        _isCreating = true;

        var webView = new WebView2();
        container.Children.Add(webView);
        try
        {
            var environment = await WebViewEnvironment.GetAsync();
            if (_host.IsPrivate)
            {
                var options = environment.CreateCoreWebView2ControllerOptions();
                options.IsInPrivateModeEnabled = true;
                options.ProfileName = WebViewEnvironment.PrivateProfileName;
                await webView.EnsureCoreWebView2Async(environment, options);
            }
            else
            {
                await webView.EnsureCoreWebView2Async(environment);
            }
        }
        catch (Exception)
        {
            container.Children.Remove(webView);
            _isCreating = false;
            return false;
        }

        _environment = await WebViewEnvironment.GetAsync();
        WebView = webView;
        Content = webView;
        _isCreating = false;
        await ConfigureAsync(webView.CoreWebView2);

        if (navigate)
        {
            Navigate(_pendingUrl ?? AppServices.Settings.HomePage);
        }
        _pendingUrl = null;
        return true;
    }

    public void Navigate(string url)
    {
        if (WebView?.CoreWebView2 is { } core)
        {
            core.Navigate(url);
        }
        else
        {
            _pendingUrl = url;
            Url = url;
        }
    }

    public void GoBack()
    {
        if (WebView?.CanGoBack == true)
        {
            WebView.GoBack();
        }
    }

    public void GoForward()
    {
        if (WebView?.CanGoForward == true)
        {
            WebView.GoForward();
        }
    }

    public void Reload() => WebView?.Reload();

    public void Stop()
    {
        WebView?.CoreWebView2?.Stop();
        IsLoading = false;
    }

    /// <summary>Shuts down the engine for this tab so its processes can exit.</summary>
    public void Close()
    {
        _sameDocumentRecordTimer?.Stop();
        _frames.Clear();
        if (WebView is { } webView)
        {
            webView.Close();
        }
        WebView = null;
        Content = null;
    }

    private async Task ConfigureAsync(CoreWebView2 core)
    {
        var settings = core.Settings;
        // No host objects. Web messages are on only for the keyboard shortcut bridge, which
        // ignores any message without this tab's secret token (see ShortcutBridge).
        settings.AreHostObjectsAllowed = false;
        settings.IsWebMessageEnabled = true;
        // A password manager is out of scope for v1, so do not store passwords or form data.
        settings.IsPasswordAutosaveEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsStatusBarEnabled = true;
        ApplyUserSettings(core);
#if !DEBUG
        // DevTools ("Inspect") is for developers; hide it from everyday users.
        settings.AreDevToolsEnabled = false;
#endif

        // Every request from every frame and worker passes the ad and tracker filter.
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.All);
        core.WebResourceRequested += Core_WebResourceRequested;
        core.NavigationStarting += Core_NavigationStarting;
        core.NavigationCompleted += Core_NavigationCompleted;
        core.SourceChanged += Core_SourceChanged;
        core.HistoryChanged += (_, _) => UpdateHistoryState();
        core.DocumentTitleChanged += Core_DocumentTitleChanged;
        core.FaviconChanged += Core_FaviconChanged;
        core.NewWindowRequested += Core_NewWindowRequested;
        core.DownloadStarting += (_, args) => _host.OnDownloadStarting(args);
        core.WebMessageReceived += (_, args) => OnWebMessage(ReadMessage(args), core.PostWebMessageAsString);
        core.FrameCreated += (_, args) => TrackFrame(args.Frame);
        await core.AddScriptToExecuteOnDocumentCreatedAsync(_bridge.Script);

        core.ProcessFailed += (_, _) =>
        {
            IsLoading = false;
            Crashed?.Invoke(this, EventArgs.Empty);
        };
    }

    /// <summary>Applies SmartScreen, tracking protection and the light/dark preference from settings.</summary>
    public void ApplyUserSettings()
    {
        if (WebView?.CoreWebView2 is { } core)
        {
            ApplyUserSettings(core);
        }
    }

    private void ApplyUserSettings(CoreWebView2 core)
    {
        var user = AppServices.Settings;
        core.Settings.IsReputationCheckingRequired = user.SmartScreen;

        var profile = core.Profile;
        profile.PreferredTrackingPreventionLevel = user.TrackingProtection switch
        {
            TrackingProtection.Basic => CoreWebView2TrackingPreventionLevel.Basic,
            TrackingProtection.Strict => CoreWebView2TrackingPreventionLevel.Strict,
            _ => CoreWebView2TrackingPreventionLevel.Balanced,
        };
        profile.PreferredColorScheme = user.Theme switch
        {
            AppTheme.Light => CoreWebView2PreferredColorScheme.Light,
            AppTheme.Dark => CoreWebView2PreferredColorScheme.Dark,
            _ => CoreWebView2PreferredColorScheme.Auto,
        };

        if (!_host.IsPrivate)
        {
            AppServices.NormalProfile ??= profile;
        }
    }


    private void Core_NavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        // Our own warning page (NavigateToString) starting to load.
        if (_loadingInterstitial)
        {
            _loadingInterstitial = false;
            _pageHost = string.Empty;
            BlockedCount = 0;
            IsLoading = true;
            return;
        }

        // Pages may only navigate the top frame to web addresses. Other schemes
        // (file:, javascript:, external protocol handlers) are refused here.
        if (!IsWebScheme(args.Uri) || !Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri))
        {
            args.Cancel = true;
            return;
        }

        // Links on a warning page ("go back", "continue anyway"). Never loaded.
        if (Interstitial.IsActionUrl(uri))
        {
            args.Cancel = true;
            if (_interstitial?.ParseAction(uri) is { } action)
            {
                OnInterstitialAction(_interstitial, action);
            }
            return;
        }

        if (TryUpgradeToHttps(sender, args, uri))
        {
            return;
        }

        if (ScamShieldService.Enabled && !_host.IsScamAllowed(uri.Host) && ScamShieldService.Check(uri) is { } verdict)
        {
            args.Cancel = true;
            ShowScamWarning(sender, args.Uri, uri.Host, verdict);
            return;
        }

        if (_upgrade is { NavigationId: null } pending && uri.Scheme == Uri.UriSchemeHttps
            && string.Equals(uri.Host, pending.Host, StringComparison.OrdinalIgnoreCase))
        {
            pending.NavigationId = args.NavigationId;
        }

        _interstitial = null;
        _mainDocumentUrl = args.Uri;
        _pageHost = uri.Host;
        _filteringOffForPage = _host.GetAdsAllowed(uri.Host) || FilterService.Engine.IsPageExcepted(args.Uri);
        BlockedCount = 0;
        IsLoading = true;
    }

    /// <summary>
    /// HTTPS-only: opens http:// addresses as https://. If the site sends us back to http://, or
    /// HTTPS fails, a warning page explains and offers "continue anyway".
    /// </summary>
    private bool TryUpgradeToHttps(CoreWebView2 core, CoreWebView2NavigationStartingEventArgs args, Uri uri)
    {
        if (!AppServices.Settings.HttpsOnly || !HttpsUpgrade.ShouldUpgrade(uri) || _host.IsHttpAllowed(uri.Host))
        {
            return false;
        }

        args.Cancel = true;
        var sameHostAgain = _upgrade is { } previous && string.Equals(previous.Host, uri.Host, StringComparison.OrdinalIgnoreCase);
        var hops = _upgrade is { } p ? p.Hops + 1 : 0;
        if ((args.IsRedirected && sameHostAgain) || hops > 3)
        {
            // The HTTPS site redirected back to http:// (or we are going round in circles).
            ShowNoHttpsWarning(core, uri.AbsoluteUri, uri.Host);
            return true;
        }

        _upgrade = new PendingUpgrade(uri.AbsoluteUri, uri.Host, hops);
        core.Navigate(HttpsUpgrade.ToHttps(uri).AbsoluteUri);
        return true;
    }

    private void Core_NavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        IsLoading = false;
        UpdateHistoryState();

        if (_upgrade is { } pending && pending.NavigationId == args.NavigationId)
        {
            _upgrade = null;
            if (!args.IsSuccess && args.WebErrorStatus is not (CoreWebView2WebErrorStatus.OperationCanceled
                    or CoreWebView2WebErrorStatus.HostNameNotResolved
                    or CoreWebView2WebErrorStatus.ValidAuthenticationCredentialsRequired
                    or CoreWebView2WebErrorStatus.ValidProxyAuthenticationRequired))
            {
                ShowNoHttpsWarning(sender, pending.HttpUrl, pending.Host);
                return;
            }
        }

        if (args.IsSuccess)
        {
            RecordVisit(sender.Source, sender.DocumentTitle);
        }
    }

    private void ShowNoHttpsWarning(CoreWebView2 core, string httpUrl, string host)
    {
        _upgrade = null;
        var warning = Interstitial.NoHttps(httpUrl, host);
        _interstitial = warning;
        _loadingInterstitial = true;
        core.NavigateToString(warning.Html);
        Url = warning.TargetUrl;
        Title = warning.Title;
        Raise(nameof(IsSecure));
    }

    private void ShowScamWarning(CoreWebView2 core, string url, string host, ScamVerdict verdict)
    {
        _upgrade = null;
        var warning = Interstitial.Scam(url, host, verdict);
        _interstitial = warning;
        _loadingInterstitial = true;
        core.NavigateToString(warning.Html);
        Url = warning.TargetUrl;
        Title = warning.Title;
        Raise(nameof(IsSecure));
    }

    private void OnInterstitialAction(Interstitial warning, InterstitialAction action)
    {
        _interstitial = null;
        if (action == InterstitialAction.Proceed)
        {
            if (warning.Kind == InterstitialKind.Scam)
            {
                _host.AllowScamSite(warning.Host);
            }
            else
            {
                _host.AllowHttp(warning.Host);
            }
            Navigate(warning.TargetUrl);
        }
        else if (WebView?.CanGoBack == true)
        {
            WebView.GoBack();
        }
        else
        {
            Navigate(AppServices.Settings.HomePage);
        }
    }

#if DEBUG
    // Developer benchmark counters.
    public int RequestCount { get; private set; }
    public static long FilterChecks { get; private set; }
    private static long _filterTicks;
    public static double FilterCheckMicroseconds => FilterChecks == 0 ? 0 : _filterTicks * 1_000_000.0 / System.Diagnostics.Stopwatch.Frequency / FilterChecks;
    public HashSet<string> PassedThirdPartyHosts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? DebugWarningNonce => _interstitial?.Nonce;
    public string? DebugWarningTarget => _interstitial?.TargetUrl;
    public void ResetRequestCount()
    {
        RequestCount = 0;
        PassedThirdPartyHosts.Clear();
    }
#endif

    /// <summary>Blocks ad and tracker requests. Runs for every request, so it must stay fast.</summary>
    private void Core_WebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
    {
#if DEBUG
        RequestCount++;
#endif
        if (!FilterService.Enabled || _filteringOffForPage || _environment is null || _pageHost.Length == 0)
        {
            return;
        }

        var url = args.Request.Uri;
        if (args.ResourceContext == CoreWebView2WebResourceContext.Document && url == _mainDocumentUrl)
        {
            return; // the page itself
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http" or "wss" or "ws"))
        {
            return;
        }

#if DEBUG
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
        var decision = FilterService.Engine.Check(url, uri.Host, _pageHost, ToRequestType(args.ResourceContext));
#if DEBUG
        _filterTicks += System.Diagnostics.Stopwatch.GetTimestamp() - started;
        FilterChecks++;
#endif
        if (decision.IsBlocked)
        {
            args.Response = _environment.CreateWebResourceResponse(null, 403, "Blocked", string.Empty);
            BlockedCount++;
        }
#if DEBUG
        else if (RegistrableDomain.IsThirdParty(uri.Host, _pageHost))
        {
            PassedThirdPartyHosts.Add(uri.Host);
        }
#endif
    }

    private static RequestType ToRequestType(CoreWebView2WebResourceContext context) => context switch
    {
        CoreWebView2WebResourceContext.Document => RequestType.Subdocument,
        CoreWebView2WebResourceContext.Stylesheet => RequestType.Stylesheet,
        CoreWebView2WebResourceContext.Image => RequestType.Image,
        CoreWebView2WebResourceContext.Media => RequestType.Media,
        CoreWebView2WebResourceContext.Font => RequestType.Font,
        CoreWebView2WebResourceContext.Script => RequestType.Script,
        CoreWebView2WebResourceContext.XmlHttpRequest or CoreWebView2WebResourceContext.Fetch
            or CoreWebView2WebResourceContext.EventSource => RequestType.XmlHttpRequest,
        CoreWebView2WebResourceContext.Websocket => RequestType.WebSocket,
        CoreWebView2WebResourceContext.Ping or CoreWebView2WebResourceContext.CspViolationReport => RequestType.Ping,
        _ => RequestType.Other,
    };

    private void Core_SourceChanged(CoreWebView2 sender, CoreWebView2SourceChangedEventArgs args)
    {
        // While a warning page is shown, keep the address the user wanted in the address bar.
        Url = _interstitial?.TargetUrl ?? sender.Source;
        Raise(nameof(IsSecure));
        if (!args.IsNewDocument)
        {
            // Same-page navigation (for example on YouTube or news sites). Sites change the title and
            // the address in either order, so wait for both to settle before recording the visit.
            _sameDocumentRecordTimer ??= CreateSameDocumentTimer();
            _sameDocumentRecordTimer.Stop();
            _sameDocumentRecordTimer.Start();
        }
    }

    private Microsoft.UI.Dispatching.DispatcherQueueTimer CreateSameDocumentTimer()
    {
        var timer = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(800);
        timer.IsRepeating = false;
        timer.Tick += (_, _) =>
        {
            if (WebView?.CoreWebView2 is { } core)
            {
                RecordVisit(core.Source, core.DocumentTitle);
            }
        };
        return timer;
    }

    private void Core_DocumentTitleChanged(CoreWebView2 sender, object args)
    {
        var title = sender.DocumentTitle;
        Title = string.IsNullOrWhiteSpace(title) ? DefaultTitle() : title;
        // Only fill in a title that was missing when the visit was recorded. Later changes belong to
        // whatever the page shows next, not to the entry already saved.
        if (_lastRecordedTitleMissing && !string.IsNullOrWhiteSpace(title) && _lastRecordedUrl == sender.Source)
        {
            _host.History?.UpdateLatestTitle(sender.Source, title);
            _lastRecordedTitleMissing = false;
        }
    }

    private async void Core_FaviconChanged(CoreWebView2 sender, object args)
    {
        try
        {
            if (string.IsNullOrEmpty(sender.FaviconUri))
            {
                Icon = DefaultIcon();
                return;
            }

            using var stream = await sender.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png);
            if (stream is null || stream.Size == 0)
            {
                Icon = DefaultIcon();
                return;
            }

            var bitmap = new BitmapImage { DecodePixelWidth = 32 };
            await bitmap.SetSourceAsync(stream);
            Favicon = bitmap;
            Icon = new ImageIconSource { ImageSource = bitmap };
        }
        catch (Exception)
        {
            // A broken favicon is not worth failing over; keep the globe icon.
            Icon = DefaultIcon();
        }
    }

    private async void Core_NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        // Popups the user did not trigger are blocked. Allowed ones open as a new tab that stays
        // connected to the opener (needed for sign-in popups).
        if (!args.IsUserInitiated || !IsWebScheme(args.Uri))
        {
            args.Handled = true;
            return;
        }

        var deferral = args.GetDeferral();
        try
        {
            var tab = await _host.OpenPopupTabAsync();
            if (tab?.WebView?.CoreWebView2 is { } popupCore)
            {
                args.NewWindow = popupCore;
            }
            args.Handled = true;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void TrackFrame(CoreWebView2Frame frame)
    {
        _frames.Add(frame);
        frame.Destroyed += (_, _) => _frames.Remove(frame);
        frame.WebMessageReceived += (_, args) => OnWebMessage(ReadMessage(args), frame.PostWebMessageAsString);
    }

    private static string? ReadMessage(CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            return args.TryGetWebMessageAsString();
        }
        catch (ArgumentException)
        {
            // Not a string: not from our script.
            return null;
        }
    }

    /// <summary>Handles a message from this tab's injected script, if it carries the tab's token.</summary>
    private void OnWebMessage(string? raw, Action<string> reply)
    {
        if (_bridge.Parse(raw) is not { } message)
        {
            return;
        }

        if (message.Shortcut is { } shortcut)
        {
            ShortcutPressed?.Invoke(this, shortcut);
            return;
        }

        switch (message.Command)
        {
            case "PhoneticHello":
                var host = CurrentHost;
                reply(_bridge.Compose("Phonetic", host is not null && _host.GetSitePhonetic(host) ? "1" : "0"));
                break;
            case "Phonetic":
                if (CurrentHost is { } site)
                {
                    _host.SetSitePhonetic(site, message.Payload == "1");
                }
                break;
            case "Suggest":
                ReplyWithSuggestions(message.Payload, reply);
                break;
        }
    }

    private void ReplyWithSuggestions(string payload, Action<string> reply)
    {
        // Payload: "<request id>:<romanised word>".
        const int MaxWord = 40;
        var colon = payload.IndexOf(':', StringComparison.Ordinal);
        if (colon is <= 0 or > 10 || !payload.AsSpan(0, colon).ToString().All(char.IsAsciiDigit))
        {
            return;
        }
        var word = payload[(colon + 1)..];
        if (word.Length is 0 or > MaxWord || !word.All(c => c is > ' ' and < '\x7f'))
        {
            return;
        }

        var list = PhoneticSuggester.Instance.Suggest(word);
        reply(_bridge.Compose("Suggest", payload[..colon] + ":" + JsonSerializer.Serialize(list)));
    }

    /// <summary>The top-level site of this tab, used as the key for per-site choices.</summary>
    private string? CurrentHost =>
        WebView?.CoreWebView2?.Source is { } source
        && Uri.TryCreate(source, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri.Host
            : null;

    /// <summary>
    /// Tells the page (all frames) that typing was switched on or off for <paramref name="host"/>,
    /// or for every site when <paramref name="host"/> is null.
    /// </summary>
    public void NotifySitePhonetic(string? host, bool enabled)
    {
        if (WebView?.CoreWebView2 is not { } core || CurrentHost is not { } current
            || (host is not null && !string.Equals(host, current, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var message = _bridge.Compose("Phonetic", enabled ? "1" : "0");
        core.PostWebMessageAsString(message);
        foreach (var frame in _frames.ToList())
        {
            try
            {
                frame.PostWebMessageAsString(message);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
            {
                // The frame is going away.
            }
        }
    }

    private void RecordVisit(string url, string title)
    {
        if (url == _lastRecordedUrl)
        {
            return;
        }
        _lastRecordedUrl = url;
        _lastRecordedTitleMissing = string.IsNullOrWhiteSpace(title);
        _host.History?.AddVisit(url, title);
    }

    private void UpdateHistoryState()
    {
        CanGoBack = WebView?.CanGoBack == true;
        CanGoForward = WebView?.CanGoForward == true;
    }

    private string DefaultTitle() => Kind switch
    {
        TabKind.History => Strings.Get("HistoryTitle"),
        TabKind.Settings => Strings.Get("SettingsTitle"),
        _ => Uri.TryCreate(_url, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host)
            ? uri.Host
            : Strings.Get("NewTabTitle"),
    };

    private IconSource DefaultIcon()
    {
        Favicon = null;
        return new FontIconSource { Glyph = Glyph, FontSize = 14 };
    }

    internal static bool IsWebScheme(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
        && (parsed.Scheme == Uri.UriSchemeHttps
            || parsed.Scheme == Uri.UriSchemeHttp
            || parsed.AbsoluteUri == "about:blank");
}
