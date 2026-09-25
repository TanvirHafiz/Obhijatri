using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using Obhijatri.App.Localization;
using Obhijatri.Core;
using Windows.System;

namespace Obhijatri.App;

public sealed partial class MainWindow : Window
{
    private const string ReloadGlyph = "";
    private const string StopGlyph = "";

    private const long ReselectWindowMs = 500;

    private bool _isLoading;
    private long _reselectUntil;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SizeToDisplay();

        ApplyStrings();
        UpdateTitle(null);
        _ = InitializeWebViewAsync();
    }

    /// <summary>Open at 80% of the work area, centred. Works the same at any DPI.</summary>
    private void SizeToDisplay()
    {
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var width = area.Width * 4 / 5;
        var height = area.Height * 4 / 5;
        AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(
            area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height));
    }

    private void ApplyStrings()
    {
        SetLabel(BackButton, "BackButtonTooltip");
        SetLabel(ForwardButton, "ForwardButtonTooltip");
        SetLabel(HomeButton, "HomeButtonTooltip");
        SetReloadState(false);

        AddressBar.PlaceholderText = Strings.Get("AddressBarPlaceholder");
        AutomationProperties.SetName(AddressBar, Strings.Get("AddressBarName"));
    }

    private static void SetLabel(FrameworkElement element, string key)
    {
        var text = Strings.Get(key);
        ToolTipService.SetToolTip(element, text);
        AutomationProperties.SetName(element, text);
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.WebViewData);
            var options = new CoreWebView2EnvironmentOptions
            {
                // Bangla for the engine's own context menus, dialogs and error pages.
                Language = BrowserDefaults.EngineLanguage,
            };
            var environment = await CoreWebView2Environment.CreateWithOptionsAsync(
                string.Empty, AppPaths.WebViewData, options);
            await WebView.EnsureCoreWebView2Async(environment);
        }
        catch (Exception)
        {
            ShowError("EngineErrorTitle", "EngineErrorMessage");
            return;
        }

        var core = WebView.CoreWebView2;
        var settings = core.Settings;
        // Bridges between pages and the app stay closed until a feature needs them.
        settings.AreHostObjectsAllowed = false;
        settings.IsWebMessageEnabled = false;
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
        core.SourceChanged += (_, _) => UpdateAddressBar();
        core.HistoryChanged += (_, _) => UpdateNavigationButtons();
        core.DocumentTitleChanged += (_, _) => UpdateTitle(core.DocumentTitle);
        core.NewWindowRequested += Core_NewWindowRequested;
        core.ProcessFailed += Core_ProcessFailed;

        Navigate(BrowserDefaults.HomeUrl);
    }

    private void Navigate(string address)
    {
        var uri = AddressResolver.Resolve(address);
        if (uri is null || WebView.CoreWebView2 is null)
        {
            return;
        }

        StatusInfoBar.IsOpen = false;
        WebView.CoreWebView2.Navigate(uri.AbsoluteUri);
        WebView.Focus(FocusState.Programmatic);
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

        SetReloadState(true);
    }

    private void Core_NavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        SetReloadState(false);
        UpdateNavigationButtons();
        UpdateAddressBar();
    }

    private void Core_NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        // Single tab until Milestone 2: open popups and target=_blank links in place.
        args.Handled = true;
        if (args.IsUserInitiated && IsWebScheme(args.Uri))
        {
            sender.Navigate(args.Uri);
        }
    }

    private void Core_ProcessFailed(CoreWebView2 sender, CoreWebView2ProcessFailedEventArgs args)
    {
        SetReloadState(false);
        ShowError("PageCrashedTitle", "PageCrashedMessage");
    }

    private static bool IsWebScheme(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
        && (parsed.Scheme == Uri.UriSchemeHttps
            || parsed.Scheme == Uri.UriSchemeHttp
            || parsed.AbsoluteUri == "about:blank");

    private void UpdateNavigationButtons()
    {
        BackButton.IsEnabled = WebView.CanGoBack;
        ForwardButton.IsEnabled = WebView.CanGoForward;
    }

    private void UpdateAddressBar()
    {
        // Do not overwrite what the user is typing.
        if (AddressBar.FocusState != FocusState.Unfocused || WebView.CoreWebView2 is null)
        {
            return;
        }

        var source = WebView.CoreWebView2.Source;
        AddressBar.Text = source == "about:blank" ? string.Empty : source;
    }

    private void UpdateTitle(string? pageTitle)
    {
        var appName = Strings.Get("AppTitle");
        Title = string.IsNullOrWhiteSpace(pageTitle) ? appName : Strings.Format("WindowTitleFormat", pageTitle, appName);
        TitleText.Text = Title;
    }

    private void SetReloadState(bool loading)
    {
        _isLoading = loading;
        ReloadIcon.Glyph = loading ? StopGlyph : ReloadGlyph;
        SetLabel(ReloadButton, loading ? "StopButtonTooltip" : "ReloadButtonTooltip");
        LoadingBar.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowError(string titleKey, string messageKey)
    {
        StatusInfoBar.Title = Strings.Get(titleKey);
        StatusInfoBar.Message = Strings.Get(messageKey);
        StatusInfoBar.IsOpen = true;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (WebView.CanGoBack)
        {
            WebView.GoBack();
        }
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (WebView.CanGoForward)
        {
            WebView.GoForward();
        }
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (WebView.CoreWebView2 is null)
        {
            return;
        }

        if (_isLoading)
        {
            WebView.CoreWebView2.Stop();
            SetReloadState(false);
        }
        else
        {
            StatusInfoBar.IsOpen = false;
            WebView.Reload();
        }
    }

    private void HomeButton_Click(object sender, RoutedEventArgs e) => Navigate(BrowserDefaults.HomeUrl);

    private void AddressBar_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        _reselectUntil = 0;
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            Navigate(AddressBar.Text);
        }
        else if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            WebView.Focus(FocusState.Programmatic);
            UpdateAddressBar();
        }
    }

    private void AddressBar_GotFocus(object sender, RoutedEventArgs e)
    {
        AddressBar.SelectAll();
        // The click that focused the box (especially one that also activates the window)
        // can place the caret after this point. Re-select if that happens shortly after.
        _reselectUntil = Environment.TickCount64 + ReselectWindowMs;
    }

    private void AddressBar_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (AddressBar.SelectionLength == 0
            && AddressBar.Text.Length > 0
            && Environment.TickCount64 < _reselectUntil)
        {
            AddressBar.SelectAll();
        }
    }

    private void FocusAddressBar_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        AddressBar.Focus(FocusState.Keyboard);
    }
}
