using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Obhijatri.App.Localization;
using Obhijatri.App.Services;

namespace Obhijatri.App.Browser;

/// <summary>Tab sleeping, pinning, memory figures and low data mode (Milestone 8).</summary>
public sealed partial class BrowserTab
{
    private const double SleepingOpacity = 0.6;

    private bool _isPinned;
    private bool _isSleeping;
    private bool _isTrimmed;
    private long _memoryBytes;
    private bool _lowData;
    private readonly LiveScript _lowDataScript = new("low-data.js");
    private readonly LiveScript _bangladeshFontScript = new("bangla-fonts.js");
    private static byte[]? _embedPlaceholder;

#if DEBUG
    public string? LastSleepDiagnostic { get; private set; }
#endif

    /// <summary>When this tab was last the one being viewed. Sleeping counts idle time from here.</summary>
    public DateTimeOffset LastActiveAt { get; set; } = TimeProvider.System.GetUtcNow();

    /// <summary>Pinned tabs stay at the start of the strip, are never put to sleep and lose their close button.</summary>
    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (Set(ref _isPinned, value))
            {
                Raise(nameof(IsClosable));
                Raise(nameof(PinMenuText));
                Raise(nameof(TooltipText));
            }
        }
    }

    public bool IsClosable => !_isPinned;

    public string PinMenuText => Strings.Get(_isPinned ? "UnpinTab" : "PinTab");

    /// <summary>True while the page is suspended to save memory. It wakes the moment the tab is shown.</summary>
    public bool IsSleeping
    {
        get => _isSleeping;
        private set
        {
            if (Set(ref _isSleeping, value))
            {
                Raise(nameof(DisplayOpacity));
                Raise(nameof(TooltipText));
            }
        }
    }

    public double DisplayOpacity => _isSleeping ? SleepingOpacity : 1.0;

    /// <summary>Private memory this tab's pages use, as of the last measurement (0 if unknown).</summary>
    public long MemoryBytes
    {
        get => _memoryBytes;
        set
        {
            if (Set(ref _memoryBytes, value))
            {
                Raise(nameof(TooltipText));
            }
        }
    }

    /// <summary>The tab's title with its memory use (or sleeping state) underneath, for the hover tip.</summary>
    public string TooltipText
    {
        get
        {
            var text = new StringBuilder(Title);
            if (_isSleeping)
            {
                text.AppendLine().Append(Strings.Get("TabSleepingTip"));
            }
            else if (_memoryBytes > 0)
            {
                text.AppendLine().Append(Strings.Format("TabMemoryFormat", Formatting.Bytes(_memoryBytes)));
            }
            return text.ToString();
        }
    }

    /// <summary>The engine's id for this page's top frame, used to match engine processes to tabs.</summary>
    public uint? MainFrameId
    {
        get
        {
            try
            {
                return WebView?.CoreWebView2?.FrameId;
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException)
            {
                return null;
            }
        }
    }

    public bool IsPlayingAudio
    {
        get
        {
            try
            {
                return WebView?.CoreWebView2?.IsDocumentPlayingAudio == true;
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException)
            {
                return false;
            }
        }
    }

    /// <summary>A web tab with a running engine. Built-in pages and tabs not yet opened have nothing to suspend.</summary>
    public bool HasEngine => Kind == TabKind.Web && WebView?.CoreWebView2 is not null;

    /// <summary>
    /// Puts the page to sleep. First choice is to suspend it: scripts and timers stop and the engine
    /// trims its memory. The engine refuses to suspend a page that is busy (open connections and
    /// the like, common on news sites), and then the page is only asked to use as little memory as
    /// it can while it keeps running. Only call for a tab that is not shown.
    /// </summary>
    public async Task<bool> TrySleepAsync(Func<bool> isShown)
    {
        if (!HasEngine || WebView is not { } view || view.CoreWebView2 is not { } core || core.IsSuspended || _isTrimmed)
        {
            return false;
        }

        try
        {
            if (await core.TrySuspendAsync())
            {
                IsSleeping = true;
                return true;
            }

            // A tab that was hidden while a newer tab was still starting up can stay "visible" as
            // far as the engine is concerned, and it then refuses to suspend. Showing it at zero
            // size for a moment and hiding it again settles that (nothing shows on screen).
            if (await BounceVisibilityAsync(view, isShown) && await core.TrySuspendAsync())
            {
                IsSleeping = true;
                return true;
            }

            core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Low;
            _isTrimmed = true;
            IsSleeping = true;
#if DEBUG
            LastSleepDiagnostic = "could not be suspended, memory trimmed instead"; // not-ui
#endif
            return true;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
#if DEBUG
            LastSleepDiagnostic = "sleep threw " + ex.GetType().Name + ": " + ex.Message; // not-ui
#endif
            return false;
        }
    }

    private static async Task<bool> BounceVisibilityAsync(WebView2 view, Func<bool> isShown)
    {
        if (view.Visibility != Visibility.Collapsed || isShown())
        {
            return false;
        }

        var (maxWidth, maxHeight) = (view.MaxWidth, view.MaxHeight);
        view.MaxWidth = 0;
        view.MaxHeight = 0;
        view.Visibility = Visibility.Visible;
        await Task.Delay(300);

        // The person may have switched to this tab meanwhile: then it must stay visible.
        var stillHidden = !isShown();
        if (stillHidden)
        {
            view.Visibility = Visibility.Collapsed;
        }
        view.MaxWidth = maxWidth;
        view.MaxHeight = maxHeight;
        if (stillHidden)
        {
            await Task.Delay(300);
        }
        return stillHidden;
    }

    /// <summary>Wakes a sleeping page. Does nothing for a tab that is awake.</summary>
    public void Wake()
    {
        if (!_isSleeping)
        {
            return;
        }

        try
        {
            if (WebView?.CoreWebView2 is { } core)
            {
                if (core.IsSuspended)
                {
                    core.Resume();
                }
                else if (_isTrimmed)
                {
                    core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Normal;
                }
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            // The engine is going away; there is nothing left to wake.
        }
        _isTrimmed = false;
        IsSleeping = false;
    }

    // ---- Low data mode ----

    /// <summary>
    /// Follows the low data setting: adds or removes the page script for pages loaded from now on
    /// and updates the flag the request filter reads (a plain field: no database work per request).
    /// </summary>
    private void ApplyLowData(CoreWebView2 core)
    {
        _lowData = AppServices.Settings.LowDataMode;
        _lowDataScript.Apply(core, _lowData);
    }

    /// <summary>
    /// A frame in another site's page that is a heavy embed (video player, social widget) is
    /// replaced by a short notice instead of loading.
    /// </summary>
    private bool TryBlockHeavyEmbed(CoreWebView2WebResourceRequestedEventArgs args, Uri uri)
    {
        if (args.ResourceContext != CoreWebView2WebResourceContext.Document
            || !Obhijatri.Safety.LowData.HeavyEmbeds.IsHeavyEmbed(uri.Host, uri.AbsolutePath)
            || !Obhijatri.Safety.RegistrableDomain.IsThirdParty(uri.Host, _pageHost))
        {
            return false;
        }

        _embedPlaceholder ??= Encoding.UTF8.GetBytes(
            "<!doctype html><meta charset=\"utf-8\"><body style=\"margin:0;display:flex;align-items:center;" // not-ui
            + "justify-content:center;height:100vh;background:#f3f3f3;color:#555;font:14px sans-serif;text-align:center\">" // not-ui
            + System.Net.WebUtility.HtmlEncode(Strings.Get("LowDataEmbedBlocked")));
        var body = new MemoryStream(_embedPlaceholder).AsRandomAccessStream();
        args.Response = _environment!.CreateWebResourceResponse(body, 200, "OK", "Content-Type: text/html; charset=utf-8"); // not-ui
        return true;
    }
}
