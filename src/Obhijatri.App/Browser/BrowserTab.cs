using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Obhijatri.App.Localization;
using Obhijatri.App.Services;
using Obhijatri.Core;
using Obhijatri.Core.Storage;

namespace Obhijatri.App.Browser;

public enum TabKind
{
    Web,
    History,
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
    private readonly ShortcutBridge _shortcuts = new();

    public BrowserTab(ITabHost host, TabKind kind, string? url, string? title)
    {
        _host = host;
        Kind = kind;
        _url = kind == TabKind.History ? InternalPages.History : url ?? string.Empty;
        _pendingUrl = kind == TabKind.Web ? url : null;
        _title = string.IsNullOrWhiteSpace(title) ? DefaultTitle() : title;
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

    /// <summary>The site icon as an image, for places that draw it directly (vertical tabs).</summary>
    public ImageSource? Favicon { get => _favicon; private set { if (Set(ref _favicon, value)) { Raise(nameof(HasFavicon)); Raise(nameof(HasNoFavicon)); } } }
    public bool HasFavicon => _favicon is not null;
    public bool HasNoFavicon => _favicon is null;

    /// <summary>Icon glyph used when there is no site icon.</summary>
    public string Glyph => Kind == TabKind.History ? "\uE81C" : "\uE774";

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

        WebView = webView;
        Content = webView;
        _isCreating = false;
        await ConfigureAsync(webView.CoreWebView2);

        if (navigate)
        {
            Navigate(_pendingUrl ?? BrowserDefaults.HomeUrl);
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
#if !DEBUG
        // DevTools ("Inspect") is for developers; hide it from everyday users.
        settings.AreDevToolsEnabled = false;
#endif

        core.NavigationStarting += Core_NavigationStarting;
        core.NavigationCompleted += Core_NavigationCompleted;
        core.SourceChanged += Core_SourceChanged;
        core.HistoryChanged += (_, _) => UpdateHistoryState();
        core.DocumentTitleChanged += Core_DocumentTitleChanged;
        core.FaviconChanged += Core_FaviconChanged;
        core.NewWindowRequested += Core_NewWindowRequested;
        core.DownloadStarting += (_, args) => _host.OnDownloadStarting(args);
        core.WebMessageReceived += (_, args) => OnWebMessage(args.TryGetWebMessageAsString());
        core.FrameCreated += (_, args) =>
            args.Frame.WebMessageReceived += (_, frameArgs) => OnWebMessage(frameArgs.TryGetWebMessageAsString());
        await core.AddScriptToExecuteOnDocumentCreatedAsync(_shortcuts.Script);

        core.ProcessFailed += (_, _) =>
        {
            IsLoading = false;
            Crashed?.Invoke(this, EventArgs.Empty);
        };
    }

    private void Core_NavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        // Pages may only navigate the top frame to web addresses. Other schemes
        // (file:, javascript:, external protocol handlers) are refused here.
        if (!IsWebScheme(args.Uri))
        {
            args.Cancel = true;
            return;
        }
        IsLoading = true;
    }

    private void Core_NavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        IsLoading = false;
        UpdateHistoryState();
        if (args.IsSuccess)
        {
            RecordVisit(sender.Source, sender.DocumentTitle);
        }
    }

    private void Core_SourceChanged(CoreWebView2 sender, CoreWebView2SourceChangedEventArgs args)
    {
        Url = sender.Source;
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

    private void OnWebMessage(string? message)
    {
        if (_shortcuts.Parse(message) is { } shortcut)
        {
            ShortcutPressed?.Invoke(this, shortcut);
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

    private string DefaultTitle() => Kind == TabKind.History
        ? Strings.Get("HistoryTitle")
        : Uri.TryCreate(_url, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host)
            ? uri.Host
            : Strings.Get("NewTabTitle");

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
