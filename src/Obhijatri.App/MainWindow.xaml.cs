using System.ComponentModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Obhijatri.App.Browser;
using Obhijatri.App.Localization;
using Obhijatri.App.Services;
using Obhijatri.Core;
using Obhijatri.Core.Storage;
using Windows.System;

namespace Obhijatri.App;

public sealed partial class MainWindow : Window, ITabHost
{
    private const string ReloadGlyph = "";
    private const string StopGlyph = "";
    private const long ReselectWindowMs = 500;

    private long _reselectUntil;

    internal MainWindow(bool isPrivate, IReadOnlyList<SessionTab>? session = null)
    {
        IsPrivate = isPrivate;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SizeToDisplay();
        ApplyStrings();

        PrivateBadge.Visibility = isPrivate ? Visibility.Visible : Visibility.Collapsed;
        MenuShowBookmarkBar.IsChecked = AppServices.Settings.GetBool(SettingKeys.ShowBookmarkBar, true);
        MenuVerticalTabs.IsChecked = AppServices.Settings.GetBool(SettingKeys.VerticalTabs, false);
        ApplyBookmarkBarVisibility();
        ApplyTabLayout();

        InitializeTabs(session);
        InitializeBookmarks();
        InitializeDownloads();

        if (AppServices.IsDatabaseTemporary)
        {
            ShowInfo(InfoBarSeverity.Warning, "DatabaseErrorTitle", "DatabaseErrorMessage");
        }

        Closed += MainWindow_Closed;
    }

    public bool IsPrivate { get; }

    public HistoryStore? History => IsPrivate ? null : AppServices.History;

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        SaveSessionNow();
        foreach (var tab in _tabs)
        {
            tab.Close();
        }
        AppServices.Bookmarks.Changed -= Bookmarks_Changed;
    }

    internal void BringToFrontWithNewTab()
    {
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
        {
            presenter.Restore();
        }
        Activate();
        NewTabFromUser();
    }

    /// <summary>Open at 80% of the work area, centred. Works the same at any DPI.</summary>
    private void SizeToDisplay()
    {
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var width = area.Width * 4 / 5;
        var height = area.Height * 4 / 5;
        // Private windows open slightly offset so they do not hide the normal window exactly.
        var offset = IsPrivate ? area.Height / 30 : 0;
        AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(
            area.X + (area.Width - width) / 2 + offset, area.Y + (area.Height - height) / 2 + offset, width, height));
    }

    private void ApplyStrings()
    {
        SetLabel(BackButton, "BackButtonTooltip");
        SetLabel(ForwardButton, "ForwardButtonTooltip");
        SetLabel(HomeButton, "HomeButtonTooltip");
        SetLabel(DownloadsButton, "DownloadsButtonTooltip");
        SetLabel(MenuButton, "MenuButtonTooltip");
        SetReloadState(false);
        SetBookmarkState(false, enabled: false);

        AddressBar.PlaceholderText = Strings.Get("AddressBarPlaceholder");
        AutomationProperties.SetName(AddressBar, Strings.Get("AddressBarName"));
        PrivateBadgeText.Text = Strings.Get("PrivateBadge");

        MenuNewTab.Text = Strings.Get("MenuNewTab");
        MenuNewPrivateWindow.Text = Strings.Get("MenuNewPrivateWindow");
        MenuHistory.Text = Strings.Get("MenuHistory");
        MenuDownloads.Text = Strings.Get("MenuDownloads");
        MenuImportBookmarks.Text = Strings.Get("MenuImportBookmarks");
        MenuShowBookmarkBar.Text = Strings.Get("MenuShowBookmarkBar");
        MenuVerticalTabs.Text = Strings.Get("MenuVerticalTabs");
        VerticalNewTabText.Text = Strings.Get("MenuNewTab");
        BookmarkBarEmptyText.Text = Strings.Get("BookmarkBarEmpty");
        DownloadsHeader.Text = Strings.Get("DownloadsTitle");
        DownloadsEmptyText.Text = Strings.Get("DownloadsEmpty");
        SetLabel(Tabs, "TabStripName");
    }

    private static void SetLabel(FrameworkElement element, string key)
    {
        var text = Strings.Get(key);
        ToolTipService.SetToolTip(element, text);
        AutomationProperties.SetName(element, text);
    }

    // ---- Toolbar state (follows the active tab) ----

    private void ActiveTab_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(BrowserTab.Url):
                UpdateAddressBar();
                UpdateBookmarkButton();
                break;
            case nameof(BrowserTab.Title):
                UpdateTitle();
                break;
            case nameof(BrowserTab.IsLoading):
                SetReloadState(_activeTab?.IsLoading == true);
                break;
            case nameof(BrowserTab.CanGoBack):
            case nameof(BrowserTab.CanGoForward):
                UpdateNavigationButtons();
                break;
        }
    }

    private void UpdateToolbar()
    {
        UpdateAddressBar();
        UpdateTitle();
        UpdateNavigationButtons();
        UpdateBookmarkButton();
        SetReloadState(_activeTab?.IsLoading == true);
        ReloadButton.IsEnabled = _activeTab?.Kind == TabKind.Web;
    }

    private void UpdateNavigationButtons()
    {
        BackButton.IsEnabled = _activeTab?.CanGoBack == true;
        ForwardButton.IsEnabled = _activeTab?.CanGoForward == true;
    }

    private void UpdateAddressBar()
    {
        // Do not overwrite what the user is typing.
        if (AddressBar.FocusState != FocusState.Unfocused)
        {
            return;
        }

        var url = _activeTab?.Url ?? string.Empty;
        AddressBar.Text = url == "about:blank" ? string.Empty : url;
    }

    private void UpdateTitle()
    {
        var appName = Strings.Get("AppTitle");
        var pageTitle = _activeTab?.Title;
        var title = string.IsNullOrWhiteSpace(pageTitle) ? appName : Strings.Format("WindowTitleFormat", pageTitle, appName);
        Title = IsPrivate ? Strings.Format("PrivateWindowTitleFormat", title) : title;
        TitleText.Text = Title;
    }

    private void SetReloadState(bool loading)
    {
        ReloadIcon.Glyph = loading ? StopGlyph : ReloadGlyph;
        SetLabel(ReloadButton, loading ? "StopButtonTooltip" : "ReloadButtonTooltip");
        LoadingBar.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowInfo(InfoBarSeverity severity, string titleKey, string message, bool messageIsKey = true)
    {
        StatusInfoBar.Severity = severity;
        StatusInfoBar.Title = Strings.Get(titleKey);
        StatusInfoBar.Message = messageIsKey ? Strings.Get(message) : message;
        StatusInfoBar.IsOpen = true;
    }

    // ---- Navigation ----

    /// <summary>Handles what the user typed: a built-in page, a web address or a search.</summary>
    private void NavigateActive(string input)
    {
        if (InternalPages.IsInternal(input?.Trim()))
        {
            if (string.Equals(input!.Trim(), InternalPages.History, StringComparison.OrdinalIgnoreCase))
            {
                OpenHistory();
            }
            return;
        }

        var uri = AddressResolver.Resolve(input);
        if (uri is null)
        {
            return;
        }

        StatusInfoBar.IsOpen = false;
        if (_activeTab?.Kind == TabKind.Web)
        {
            _activeTab.Navigate(uri.AbsoluteUri);
            _activeTab.WebView?.Focus(FocusState.Programmatic);
        }
        else
        {
            OpenTab(uri.AbsoluteUri);
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => _activeTab?.GoBack();

    private void ForwardButton_Click(object sender, RoutedEventArgs e) => _activeTab?.GoForward();

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTab is null)
        {
            return;
        }

        if (_activeTab.IsLoading)
        {
            _activeTab.Stop();
        }
        else
        {
            StatusInfoBar.IsOpen = false;
            _activeTab.Reload();
        }
    }

    private void HomeButton_Click(object sender, RoutedEventArgs e) => NavigateActive(BrowserDefaults.HomeUrl);

    private void AddressBar_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        _reselectUntil = 0;
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            NavigateActive(AddressBar.Text);
        }
        else if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            _activeTab?.Content?.Focus(FocusState.Programmatic);
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

    private void FocusAddressBar()
    {
        AddressBar.Focus(FocusState.Keyboard);
        AddressBar.SelectAll();
    }

    // ---- Menu and keyboard shortcuts ----

    private void MenuNewTab_Click(object sender, RoutedEventArgs e) => NewTabFromUser();

    private void MenuNewPrivateWindow_Click(object sender, RoutedEventArgs e) => App.OpenPrivateWindow();

    private void MenuHistory_Click(object sender, RoutedEventArgs e) => OpenHistory();

    private void MenuDownloads_Click(object sender, RoutedEventArgs e) => ShowDownloads();

    private void MenuShowBookmarkBar_Click(object sender, RoutedEventArgs e)
    {
        AppServices.Settings.SetBool(SettingKeys.ShowBookmarkBar, MenuShowBookmarkBar.IsChecked);
        ApplyBookmarkBarVisibility();
    }

    private void MenuVerticalTabs_Click(object sender, RoutedEventArgs e)
    {
        AppServices.Settings.SetBool(SettingKeys.VerticalTabs, MenuVerticalTabs.IsChecked);
        ApplyTabLayout();
    }

    private void NewTab_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        NewTabFromUser();
    }

    private void CloseTab_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (_activeTab is not null)
        {
            CloseTab(_activeTab);
        }
    }

    private void ReopenTab_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        ReopenClosedTab();
    }

    private void NextTab_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        SelectRelativeTab(+1);
    }

    private void PreviousTab_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        SelectRelativeTab(-1);
    }

    private void NewPrivateWindow_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        App.OpenPrivateWindow();
    }

    private void History_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        OpenHistory();
    }

    private void Downloads_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        ShowDownloads();
    }

    private void ToggleBookmarkBar_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        ToggleBookmarkBar();
    }

    private void ToggleBookmarkBar()
    {
        MenuShowBookmarkBar.IsChecked = !MenuShowBookmarkBar.IsChecked;
        MenuShowBookmarkBar_Click(MenuShowBookmarkBar, new RoutedEventArgs());
    }

    private void Bookmark_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        ShowBookmarkEditor();
    }

    private void FocusAddressBar_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        FocusAddressBar();
    }
}
