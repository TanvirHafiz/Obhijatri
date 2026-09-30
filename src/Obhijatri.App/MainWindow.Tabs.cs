using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Obhijatri.App.Browser;
using Obhijatri.App.Localization;
using Obhijatri.App.Services;
using Obhijatri.App.Views;
using Obhijatri.Core;
using Obhijatri.Core.Storage;

namespace Obhijatri.App;

public sealed partial class MainWindow
{
    private readonly ObservableCollection<BrowserTab> _tabs = [];
    private readonly ClosedTabStack _closedTabs = new();
    private BrowserTab? _activeTab;
    private bool _syncingSelection;
    private DispatcherQueueTimer? _sessionSaveTimer;

    private void InitializeTabs(IReadOnlyList<SessionTab>? session)
    {
        Tabs.TabItemsSource = _tabs;
        VerticalTabList.ItemsSource = _tabs;
        _tabs.CollectionChanged += Tabs_CollectionChanged;

        // Middle-click closes a tab. The controls mark pointer events handled, so listen to those too.
        Tabs.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(TabStrip_PointerReleased), true);
        VerticalTabList.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(TabStrip_PointerReleased), true);

        if (!IsPrivate)
        {
            _sessionSaveTimer = DispatcherQueue.CreateTimer();
            _sessionSaveTimer.Interval = TimeSpan.FromSeconds(1);
            _sessionSaveTimer.IsRepeating = false;
            _sessionSaveTimer.Tick += (_, _) => SaveSessionNow();
        }

        BrowserTab? active = null;
        foreach (var saved in session ?? [])
        {
            var kind = KindOf(saved.Url);
            if (kind == TabKind.Web && !BrowserTab.IsWebScheme(saved.Url))
            {
                continue;
            }

            // Lazy restore: the tab is listed but its engine is only created when it is first shown.
            var restored = new BrowserTab(this, kind, saved.Url, saved.Title) { IsPinned = saved.IsPinned };
            var tab = AddTab(restored, _tabs.Count);
            if (saved.IsActive)
            {
                active = tab;
            }
        }

        if (_tabs.Count == 0)
        {
            OpenNewTabPage();
        }
        else
        {
            _ = ActivateTabAsync(active ?? _tabs[0]);
        }
    }

    private BrowserTab AddTab(BrowserTab tab, int index)
    {
        _tabs.Insert(Math.Clamp(index, 0, _tabs.Count), tab);
        tab.PropertyChanged += AnyTab_PropertyChanged;
        tab.Crashed += Tab_Crashed;
        tab.ShortcutPressed += Tab_ShortcutPressed;
        return tab;
    }

    /// <summary>Opens a web tab next to the active one and shows it.</summary>
    private BrowserTab OpenTab(string url)
    {
        var tab = AddTab(new BrowserTab(this, TabKind.Web, url, null), NextTabIndex());
        _ = ActivateTabAsync(tab);
        return tab;
    }

    private void NewTabFromUser()
    {
        OpenNewTabPage();
        FocusAddressBar();
    }

    /// <summary>Opens the new tab page next to the active tab and shows it.</summary>
    private BrowserTab OpenNewTabPage()
    {
        var tab = AddTab(new BrowserTab(this, TabKind.NewTab, null, null), NextTabIndex());
        _ = ActivateTabAsync(tab);
        return tab;
    }

    /// <summary>
    /// Opens an address from the new tab page (a speed dial tile) or the address bar while a new
    /// tab page is showing: the address takes the new tab's place instead of opening one beside it.
    /// </summary>
    private void OpenInPlaceOfNewTab(BrowserTab newTabPage, string url)
    {
        var index = _tabs.IndexOf(newTabPage);
        var tab = AddTab(new BrowserTab(this, TabKind.Web, url, null), index < 0 ? _tabs.Count : index + 1);
        _ = ActivateTabAsync(tab);
        CloseTab(newTabPage);
    }

    private int NextTabIndex() => _activeTab is null ? _tabs.Count : _tabs.IndexOf(_activeTab) + 1;

    private void OpenHistory() => OpenInternalPage(TabKind.History);

    private void OpenSettings() => OpenInternalPage(TabKind.Settings);

    private void OpenAbout() => OpenInternalPage(TabKind.About);

    /// <summary>Shows the built-in page, reusing its tab if one is already open.</summary>
    private void OpenInternalPage(TabKind kind)
    {
        var existing = _tabs.FirstOrDefault(t => t.Kind == kind);
        if (existing is not null)
        {
            (existing.Content as HistoryView)?.Refresh();
            _ = ActivateTabAsync(existing);
            return;
        }

        _ = ActivateTabAsync(AddTab(new BrowserTab(this, kind, null, null), NextTabIndex()));
    }

    private static TabKind KindOf(string url) =>
        string.Equals(url, InternalPages.History, StringComparison.OrdinalIgnoreCase) ? TabKind.History
        : string.Equals(url, InternalPages.Settings, StringComparison.OrdinalIgnoreCase) ? TabKind.Settings
        : string.Equals(url, InternalPages.NewTab, StringComparison.OrdinalIgnoreCase) ? TabKind.NewTab
        : string.Equals(url, InternalPages.About, StringComparison.OrdinalIgnoreCase) ? TabKind.About
        : TabKind.Web;

    /// <summary>Shows <paramref name="tab"/>, creating its content the first time.</summary>
    private async Task ActivateTabAsync(BrowserTab tab)
    {
        if (!_tabs.Contains(tab))
        {
            return;
        }

        if (_activeTab != tab)
        {
            CloseReader();
            if (_activeTab is not null)
            {
                _activeTab.PropertyChanged -= ActiveTab_PropertyChanged;
                // Idle time for tab sleeping counts from the moment the tab is left.
                _activeTab.LastActiveAt = TimeProvider.System.GetUtcNow();
            }
            _activeTab = tab;
            tab.PropertyChanged += ActiveTab_PropertyChanged;
        }

        // A sleeping tab wakes as soon as it is chosen, before it is shown.
        tab.Wake();

        SyncSelection();
        ShowOnlyActiveContent();
        UpdateToolbar();
        ScheduleSessionSave();

        if (!tab.IsCreated)
        {
            if (tab.Kind != TabKind.Web)
            {
                FrameworkElement view = tab.Kind switch
                {
                    TabKind.History => new HistoryView(AppServices.History, url => OpenTab(url), ClearEngineHistoryAsync),
                    TabKind.NewTab => new NewTabView(History, url => OpenInPlaceOfNewTab(tab, url)),
                    TabKind.About => new AboutView(),
                    _ => new SettingsView(OpenHistory, ClearSiteDataAsync),
                };
                tab.SetContent(view);
                ContentHost.Children.Add(view);
            }
            else if (!await tab.CreateWebViewAsync(ContentHost))
            {
                ShowInfo(InfoBarSeverity.Error, "EngineErrorTitle", "EngineErrorMessage");
                return;
            }

            // The user may have switched tabs while the engine was starting.
            ShowOnlyActiveContent();
            UpdateToolbar();
        }
    }

    private void ShowOnlyActiveContent()
    {
        // While reader mode is open its view covers the page.
        var active = _readerView is null ? _activeTab?.Content : null;
        foreach (var child in ContentHost.Children)
        {
            child.Visibility = child == active || child == _readerView ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void CloseTab(BrowserTab tab)
    {
        var index = _tabs.IndexOf(tab);
        if (index < 0)
        {
            return;
        }

        if (tab.Kind != TabKind.NewTab && (tab.Kind != TabKind.Web || BrowserTab.IsWebScheme(tab.Url)))
        {
            _closedTabs.Push(new ClosedTab(tab.Url, tab.Title, index));
        }

        var wasActive = tab == _activeTab;
        _syncingSelection = true;
        try
        {
            _tabs.RemoveAt(index);
        }
        finally
        {
            _syncingSelection = false;
        }

        tab.PropertyChanged -= AnyTab_PropertyChanged;
        tab.PropertyChanged -= ActiveTab_PropertyChanged;
        tab.Crashed -= Tab_Crashed;
        tab.ShortcutPressed -= Tab_ShortcutPressed;
        if (tab.Content is { } content)
        {
            ContentHost.Children.Remove(content);
        }
        tab.Close();

        if (_tabs.Count == 0)
        {
            _activeTab = null;
            // Closing the last tab closes the window, like other browsers.
            Close();
            return;
        }

        if (wasActive)
        {
            _activeTab = null;
            _ = ActivateTabAsync(_tabs[Math.Min(index, _tabs.Count - 1)]);
        }
        else
        {
            SyncSelection();
        }
    }

    private void ReopenClosedTab()
    {
        if (!_closedTabs.TryPop(out var closed) || closed is null)
        {
            return;
        }

        var kind = KindOf(closed.Url);
        if (kind != TabKind.Web)
        {
            OpenInternalPage(kind);
            return;
        }

        var tab = AddTab(new BrowserTab(this, kind, closed.Url, closed.Title), closed.Index);
        _ = ActivateTabAsync(tab);
    }

    private void SelectRelativeTab(int step)
    {
        if (_activeTab is null || _tabs.Count < 2)
        {
            return;
        }
        var index = (_tabs.IndexOf(_activeTab) + step + _tabs.Count) % _tabs.Count;
        _ = ActivateTabAsync(_tabs[index]);
    }

    /// <summary>Makes both tab strips show the active tab as selected.</summary>
    private void SyncSelection()
    {
        _syncingSelection = true;
        try
        {
            Tabs.SelectedItem = _activeTab;
            VerticalTabList.SelectedItem = _activeTab;
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void Tabs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // A drag reorder removes and re-inserts the item, which can drop the selection.
        if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Move)
        {
            DispatcherQueue.TryEnqueue(() => SyncSelection());
        }
        UpdateTabCount();
        ScheduleSessionSave();
    }

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncingSelection && Tabs.SelectedItem is BrowserTab tab && tab != _activeTab)
        {
            _ = ActivateTabAsync(tab);
        }
    }

    private void VerticalTabList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncingSelection && VerticalTabList.SelectedItem is BrowserTab tab && tab != _activeTab)
        {
            _ = ActivateTabAsync(tab);
        }
    }

    private void Tabs_AddTabButtonClick(TabView sender, object args) => NewTabFromUser();

    private void VerticalNewTabButton_Click(object sender, RoutedEventArgs e) => NewTabFromUser();

    private void Tabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Item is BrowserTab tab)
        {
            CloseTab(tab);
        }
    }

    private void VerticalTabClose_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is BrowserTab tab)
        {
            CloseTab(tab);
        }
    }

    private void TabStrip_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(sender as UIElement);
        if (point.Properties.PointerUpdateKind != PointerUpdateKind.MiddleButtonReleased)
        {
            return;
        }

        for (var element = e.OriginalSource as DependencyObject; element is not null; element = VisualTreeHelper.GetParent(element))
        {
            var tab = element switch
            {
                TabViewItem tabItem => tabItem.DataContext as BrowserTab,
                ListViewItem listItem => VerticalTabList.ItemFromContainer(listItem) as BrowserTab,
                _ => null,
            };
            if (tab is not null)
            {
                e.Handled = true;
                CloseTab(tab);
                return;
            }
        }
    }

    private void Tab_Crashed(object? sender, EventArgs e)
    {
        if (sender == _activeTab)
        {
            ShowInfo(InfoBarSeverity.Error, "PageCrashedTitle", "PageCrashedMessage");
        }
    }

    private void Tab_ShortcutPressed(object? sender, BrowserShortcut shortcut)
    {
        if (sender is not BrowserTab tab || tab != _activeTab)
        {
            return;
        }

        switch (shortcut)
        {
            case BrowserShortcut.NewTab: NewTabFromUser(); break;
            case BrowserShortcut.CloseTab: CloseTab(tab); break;
            case BrowserShortcut.ReopenTab: ReopenClosedTab(); break;
            case BrowserShortcut.NextTab: SelectRelativeTab(+1); break;
            case BrowserShortcut.PreviousTab: SelectRelativeTab(-1); break;
            case BrowserShortcut.FocusAddressBar: FocusAddressBar(); break;
            case BrowserShortcut.History: OpenHistory(); break;
            case BrowserShortcut.Settings: OpenSettings(); break;
            case BrowserShortcut.Downloads: ShowDownloads(); break;
            case BrowserShortcut.Bookmark: ShowBookmarkEditor(); break;
            case BrowserShortcut.ToggleBookmarkBar: ToggleBookmarkBar(); break;
            case BrowserShortcut.PrivateWindow: App.OpenPrivateWindow(); break;
        }
    }

    // ---- Popups (ITabHost) ----

    public async Task<BrowserTab?> OpenPopupTabAsync()
    {
        var tab = AddTab(new BrowserTab(this, TabKind.Web, null, null), NextTabIndex());
        if (!await tab.CreateWebViewAsync(ContentHost, navigate: false))
        {
            CloseTab(tab);
            return null;
        }
        await ActivateTabAsync(tab);
        return tab;
    }

    // ---- Layout ----

    private void ApplyTabLayout()
    {
        var vertical = AppServices.Settings.VerticalTabs;
        MenuVerticalTabs.IsChecked = vertical;
        Tabs.Visibility = vertical ? Visibility.Collapsed : Visibility.Visible;
        PlainTitleBar.Visibility = vertical ? Visibility.Visible : Visibility.Collapsed;
        VerticalTabsPanel.Visibility = vertical ? Visibility.Visible : Visibility.Collapsed;
        SetTitleBar(vertical ? PlainTitleBar : TabDragRegion);
        SyncSelection();
    }

    private void UpdateTabCount() =>
        TabCountText.Text = Strings.Format("TabCountFormat", Formatting.Number(_tabs.Count));

    // ---- Session ----

    private void AnyTab_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BrowserTab.Url) or nameof(BrowserTab.Title))
        {
            ScheduleSessionSave();
        }
    }

    private void ScheduleSessionSave()
    {
        if (_sessionSaveTimer is null)
        {
            return;
        }
        _sessionSaveTimer.Stop();
        _sessionSaveTimer.Start();
    }

    /// <summary>Private windows never save their tabs.</summary>
    internal void SaveSessionNow()
    {
        if (IsPrivate)
        {
            return;
        }

        _sessionSaveTimer?.Stop();
        var tabs = _tabs
            .Where(t => t.Kind != TabKind.Web || BrowserTab.IsWebScheme(t.Url))
            .Select(t => new SessionTab(t.Url, t.Title, t == _activeTab, t.IsPinned))
            .ToList();
        AppServices.Sessions.Save(tabs);
    }

    // ---- History (engine side) ----

    /// <summary>Clears the engine's own browsing history, used for visited-link colours.</summary>
    private async Task ClearEngineHistoryAsync(TimeSpan? span)
    {
        var profile = _tabs.Select(t => t.WebView?.CoreWebView2?.Profile).FirstOrDefault(p => p is not null);
        if (profile is null)
        {
            return;
        }

        const Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds kinds =
            Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.BrowsingHistory;
        if (span is TimeSpan s)
        {
            var end = DateTime.Now;
            await profile.ClearBrowsingDataAsync(kinds, end - s, end);
        }
        else
        {
            await profile.ClearBrowsingDataAsync(kinds);
        }
    }
}
