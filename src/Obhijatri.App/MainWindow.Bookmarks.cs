using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.Storage.Pickers;
using Obhijatri.App.Browser;
using Obhijatri.App.Localization;
using Obhijatri.App.Services;
using Obhijatri.Core.Bookmarks;
using Obhijatri.Core.Storage;

namespace Obhijatri.App;

public sealed partial class MainWindow
{
    private const double BookmarkMaxWidth = 180;

    private void InitializeBookmarks()
    {
        AppServices.Bookmarks.Changed += Bookmarks_Changed;
        RebuildBookmarkBar();
    }

    private void Bookmarks_Changed(object? sender, EventArgs e)
    {
        RebuildBookmarkBar();
        UpdateBookmarkButton();
    }

    private void ApplyBookmarkBarVisibility()
    {
        var show = AppServices.Settings.ShowBookmarkBar;
        MenuShowBookmarkBar.IsChecked = show;
        BookmarkBar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---- Bookmark bar ----

    private void RebuildBookmarkBar()
    {
        BookmarkBarItems.Children.Clear();
        var items = AppServices.Bookmarks.GetChildren(null);
        foreach (var item in items)
        {
            BookmarkBarItems.Children.Add(item.IsFolder ? CreateFolderButton(item) : CreateLinkButton(item));
        }
        BookmarkBarEmptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private Button CreateLinkButton(Bookmark bookmark)
    {
        var button = new Button
        {
            Content = BarItemContent("", bookmark.Title),
            Tag = bookmark,
            Padding = new Thickness(8, 3, 8, 3),
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
        };
        ToolTipService.SetToolTip(button, $"{bookmark.Title}\n{bookmark.Url}");
        button.Click += (_, _) => OpenBookmark(bookmark, newTab: IsControlDown());
        button.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((s, e) =>
        {
            if (e.GetCurrentPoint(button).Properties.PointerUpdateKind == PointerUpdateKind.MiddleButtonReleased)
            {
                e.Handled = true;
                OpenBookmark(bookmark, newTab: true);
            }
        }), true);
        button.ContextFlyout = CreateItemContextMenu(bookmark);
        return button;
    }

    private DropDownButton CreateFolderButton(Bookmark folder)
    {
        var menu = new MenuFlyout { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft };
        // Build the menu when it opens, so large imported folders do not slow the bar down.
        menu.Opening += (_, _) =>
        {
            menu.Items.Clear();
            FillFolderMenu(menu.Items, folder.Id);
        };

        var button = new DropDownButton
        {
            Content = BarItemContent("", folder.Title),
            Tag = folder,
            Flyout = menu,
            Padding = new Thickness(8, 3, 8, 3),
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
        };
        ToolTipService.SetToolTip(button, folder.Title);
        button.ContextFlyout = CreateItemContextMenu(folder);
        return button;
    }

    private void FillFolderMenu(IList<MenuFlyoutItemBase> target, long folderId)
    {
        var children = AppServices.Bookmarks.GetChildren(folderId);
        if (children.Count == 0)
        {
            target.Add(new MenuFlyoutItem { Text = Strings.Get("BookmarkFolderEmpty"), IsEnabled = false });
            return;
        }

        foreach (var child in children)
        {
            if (child.IsFolder)
            {
                var sub = new MenuFlyoutSubItem { Text = child.Title, Icon = new FontIcon { Glyph = "" } };
                FillFolderMenu(sub.Items, child.Id);
                target.Add(sub);
            }
            else
            {
                var item = new MenuFlyoutItem { Text = child.Title, Icon = new FontIcon { Glyph = "" } };
                ToolTipService.SetToolTip(item, child.Url);
                item.Click += (_, _) => OpenBookmark(child, newTab: IsControlDown());
                target.Add(item);
            }
        }
    }

    private static StackPanel BarItemContent(string glyph, string title)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        panel.Children.Add(new FontIcon { Glyph = glyph, FontSize = 12 });
        panel.Children.Add(new TextBlock
        {
            Text = title,
            MaxWidth = BookmarkMaxWidth,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
        });
        return panel;
    }

    private MenuFlyout CreateItemContextMenu(Bookmark item)
    {
        var menu = new MenuFlyout();
        if (!item.IsFolder)
        {
            var openNew = new MenuFlyoutItem { Text = Strings.Get("BookmarkOpenInNewTab") };
            openNew.Click += (_, _) => OpenBookmark(item, newTab: true);
            menu.Items.Add(openNew);
            menu.Items.Add(new MenuFlyoutSeparator());
        }

        var rename = new MenuFlyoutItem { Text = Strings.Get("BookmarkRename") };
        rename.Click += async (_, _) =>
        {
            var name = await PromptAsync("BookmarkRenameTitle", item.Title);
            if (!string.IsNullOrWhiteSpace(name))
            {
                AppServices.Bookmarks.Rename(item.Id, name);
            }
        };
        menu.Items.Add(rename);

        var newFolder = new MenuFlyoutItem { Text = Strings.Get("BookmarkNewFolder") };
        newFolder.Click += async (_, _) => await CreateFolderAsync();
        menu.Items.Add(newFolder);

        var delete = new MenuFlyoutItem { Text = Strings.Get("BookmarkDelete"), Icon = new FontIcon { Glyph = "" } };
        delete.Click += (_, _) => AppServices.Bookmarks.Delete(item.Id);
        menu.Items.Add(delete);
        return menu;
    }

    private void BookmarkBar_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }
        e.Handled = true;

        var menu = new MenuFlyout();
        var newFolder = new MenuFlyoutItem { Text = Strings.Get("BookmarkNewFolder"), Icon = new FontIcon { Glyph = "" } };
        newFolder.Click += async (_, _) => await CreateFolderAsync();
        menu.Items.Add(newFolder);
        menu.ShowAt(BookmarkBar, e.GetPosition(BookmarkBar));
    }

    private async Task CreateFolderAsync()
    {
        var name = await PromptAsync("BookmarkNewFolderTitle", string.Empty);
        if (!string.IsNullOrWhiteSpace(name))
        {
            AppServices.Bookmarks.AddFolder(null, name);
        }
    }

    private void OpenBookmark(Bookmark bookmark, bool newTab)
    {
        if (bookmark.Url is null)
        {
            return;
        }

        if (newTab || _activeTab?.Kind != TabKind.Web)
        {
            OpenTab(bookmark.Url);
        }
        else
        {
            _activeTab.Navigate(bookmark.Url);
        }
    }

    private static bool IsControlDown() =>
        InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    // ---- Star button and bookmark editor ----

    private void UpdateBookmarkButton()
    {
        var url = _activeTab?.Url;
        var isWeb = _activeTab?.Kind == TabKind.Web && url is not null && BrowserTab.IsWebScheme(url) && url != "about:blank";
        SetBookmarkState(isWeb && AppServices.Bookmarks.IsBookmarked(url!), isWeb);
    }

    private void SetBookmarkState(bool bookmarked, bool enabled)
    {
        BookmarkButton.IsEnabled = enabled;
        BookmarkIcon.Glyph = bookmarked ? "" : "";
        SetLabel(BookmarkButton, bookmarked ? "BookmarkEditTooltip" : "BookmarkAddTooltip");
    }

    private void BookmarkButton_Click(object sender, RoutedEventArgs e) => ShowBookmarkEditor();

    /// <summary>
    /// Star: bookmarks the page (on the bar) straight away, then shows a small editor to rename it,
    /// pick a folder, or remove it again.
    /// </summary>
    private void ShowBookmarkEditor()
    {
        if (_activeTab is not { Kind: TabKind.Web } tab || !BookmarkButton.IsEnabled)
        {
            return;
        }

        var url = tab.Url;
        var store = AppServices.Bookmarks;
        var existing = FindBookmark(url);
        var id = existing?.Id ?? store.AddBookmark(null, tab.Title, url);
        var current = store.Get(id)!;

        var nameBox = new TextBox { Header = Strings.Get("BookmarkNameLabel"), Text = current.Title };
        var folders = new List<(long? Id, string Title)> { (null, Strings.Get("BookmarkBarFolderName")) };
        folders.AddRange(store.GetAllFolders().Select(f => ((long?)f.Id, f.Title)));
        var folderBox = new ComboBox
        {
            Header = Strings.Get("BookmarkFolderLabel"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = folders.Select(f => f.Title).ToList(),
            SelectedIndex = Math.Max(0, folders.FindIndex(f => f.Id == current.ParentId)),
        };

        var flyout = new Flyout { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight };
        var done = new Button { Content = Strings.Get("BookmarkDone"), Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        var remove = new Button { Content = Strings.Get("BookmarkRemove") };
        var removed = false;
        remove.Click += (_, _) =>
        {
            removed = true;
            store.RemoveByUrl(url);
            flyout.Hide();
        };
        done.Click += (_, _) => flyout.Hide();
        flyout.Closed += (_, _) =>
        {
            if (removed || store.Get(id) is null)
            {
                return;
            }
            if (!string.IsNullOrWhiteSpace(nameBox.Text) && nameBox.Text.Trim() != current.Title)
            {
                store.Rename(id, nameBox.Text);
            }
            var folder = folders[Math.Max(0, folderBox.SelectedIndex)].Id;
            if (folder != current.ParentId)
            {
                store.Move(id, folder, int.MaxValue);
            }
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(remove);
        buttons.Children.Add(done);
        var panel = new StackPanel { Width = 320, Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = Strings.Get(existing is null ? "BookmarkAddedTitle" : "BookmarkEditTitle"),
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
        });
        panel.Children.Add(nameBox);
        panel.Children.Add(folderBox);
        panel.Children.Add(buttons);
        flyout.Content = panel;
        flyout.ShowAt(BookmarkButton);
    }

    private static Bookmark? FindBookmark(string url)
    {
        if (!AppServices.Bookmarks.IsBookmarked(url))
        {
            return null;
        }
        return FindIn(null);

        Bookmark? FindIn(long? parent)
        {
            foreach (var item in AppServices.Bookmarks.GetChildren(parent))
            {
                if (!item.IsFolder && item.Url == url)
                {
                    return item;
                }
                if (item.IsFolder && FindIn(item.Id) is { } found)
                {
                    return found;
                }
            }
            return null;
        }
    }

    // ---- Import ----

    private async void MenuImportBookmarks_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker(AppWindow.Id);
        picker.FileTypeFilter.Add(".html");
        picker.FileTypeFilter.Add(".htm");
        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        BookmarkImportResult result;
        try
        {
            var info = new FileInfo(file.Path);
            if (info.Length > BookmarkHtmlImporter.MaxFileBytes)
            {
                ShowInfo(InfoBarSeverity.Error, "ImportFailedTitle", "ImportTooLarge");
                return;
            }
            var html = await File.ReadAllTextAsync(file.Path);
            result = await Task.Run(() => BookmarkHtmlImporter.Parse(html));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            ShowInfo(InfoBarSeverity.Error, "ImportFailedTitle", "ImportReadError");
            return;
        }

        if (result.BookmarkCount == 0)
        {
            ShowInfo(InfoBarSeverity.Warning, "ImportFailedTitle", "ImportNothingFound");
            return;
        }

        var added = AppServices.Bookmarks.Import(result.Items, Strings.Get("ImportFolderName"));
        AppServices.Settings.ShowBookmarkBar = true;
        var message = Strings.Format("ImportDoneMessage", Formatting.Number(added));
        if (result.SkippedCount > 0)
        {
            message += " " + Strings.Format("ImportSkippedMessage", Formatting.Number(result.SkippedCount));
        }
        ShowInfo(InfoBarSeverity.Success, "ImportDoneTitle", message, messageIsKey: false);
    }

    // ---- Small text prompt ----

    private async Task<string?> PromptAsync(string titleKey, string initial)
    {
        var box = new TextBox { Text = initial, MaxLength = BookmarkStore.MaxTitleLength };
        box.Loaded += (_, _) => box.SelectAll();
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = Strings.Get(titleKey),
            Content = box,
            PrimaryButtonText = Strings.Get("DialogSave"),
            CloseButtonText = Strings.Get("DialogCancel"),
            DefaultButton = ContentDialogButton.Primary,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? box.Text.Trim() : null;
    }
}
