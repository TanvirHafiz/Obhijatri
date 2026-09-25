using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Obhijatri.App.Localization;
using Obhijatri.App.Services;
using Obhijatri.Core;
using Obhijatri.Core.Settings;

namespace Obhijatri.App.Views;

/// <summary>
/// The built-in settings page: সাধারণ, নিরাপত্তা, গোপনীয়তা, চেহারা, উন্নত.
/// Built in code so that every label comes from the resource files.
/// </summary>
public sealed partial class SettingsView : UserControl
{
    private readonly BrowserSettings _settings = AppServices.Settings;
    private readonly Action _openHistory;
    private readonly Func<Task<bool>> _clearSiteData;
    private readonly List<(string TitleKey, StackPanel Panel)> _sections = [];

    // Controls that must follow changes made elsewhere (for example from the menu).
    private ToggleSwitch? _bookmarkBarToggle;
    private ToggleSwitch? _verticalTabsToggle;
    private bool _refreshing;

    internal SettingsView(Action openHistory, Func<Task<bool>> clearSiteData)
    {
        _openHistory = openHistory;
        _clearSiteData = clearSiteData;
        InitializeComponent();

        AddSection("SettingsGeneral", "", BuildGeneral());
        AddSection("SettingsSecurity", "", BuildSecurity());
        AddSection("SettingsPrivacy", "", BuildPrivacy());
        AddSection("SettingsAppearance", "", BuildAppearance());
        AddSection("SettingsAdvanced", "", BuildAdvanced());
        Nav.SelectedItem = Nav.MenuItems[0];

        Loaded += (_, _) => _settings.Changed += Settings_Changed;
        Unloaded += (_, _) => _settings.Changed -= Settings_Changed;
    }

    private void AddSection(string titleKey, string glyph, StackPanel panel)
    {
        _sections.Add((titleKey, panel));
        Nav.MenuItems.Add(new NavigationViewItem
        {
            Content = Strings.Get(titleKey),
            Icon = new FontIcon { Glyph = glyph },
            Tag = _sections.Count - 1,
        });
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem { Tag: int index })
        {
            SectionTitle.Text = Strings.Get(_sections[index].TitleKey);
            SectionHost.Content = _sections[index].Panel;
            SectionScroller.ChangeView(null, 0, null, disableAnimation: true);
        }
    }

    private void Settings_Changed(object? sender, string key)
    {
        _refreshing = true;
        try
        {
            if (_bookmarkBarToggle is not null)
            {
                _bookmarkBarToggle.IsOn = _settings.ShowBookmarkBar;
            }
            if (_verticalTabsToggle is not null)
            {
                _verticalTabsToggle.IsOn = _settings.VerticalTabs;
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    // ---- সাধারণ (General) ----

    private StackPanel BuildGeneral()
    {
        var panel = NewPanel();

        // Language: applies after a restart.
        var restartBar = new InfoBar
        {
            Severity = InfoBarSeverity.Informational,
            Title = Strings.Get("SettingsLanguageRestartTitle"),
            Message = Strings.Get("SettingsLanguageRestartMessage"),
            IsClosable = false,
            IsOpen = false,
        };
        var restartButton = new Button { Content = Strings.Get("SettingsRestartNow") };
        restartButton.Click += (_, _) => App.Restart();
        restartBar.ActionButton = restartButton;

        var startupLanguage = _settings.UiLanguage;
        var languages = new RadioButtons();
        languages.Items.Add(Strings.Get("LanguageBangla"));
        languages.Items.Add(Strings.Get("LanguageEnglish"));
        languages.SelectedIndex = _settings.IsBangla ? 0 : 1;
        languages.SelectionChanged += (_, _) =>
        {
            _settings.UiLanguage = languages.SelectedIndex == 0 ? BrowserSettings.Bangla : BrowserSettings.English;
            restartBar.IsOpen = _settings.UiLanguage != startupLanguage;
        };
        panel.Children.Add(Card("SettingsLanguage", "SettingsLanguageDescription", languages, stacked: true));
        panel.Children.Add(restartBar);

        // Home page.
        var homeBox = new TextBox
        {
            Text = _settings.HomePage,
            MinWidth = 320,
            IsSpellCheckEnabled = false,
            InputScope = new Microsoft.UI.Xaml.Input.InputScope { Names = { new Microsoft.UI.Xaml.Input.InputScopeName(Microsoft.UI.Xaml.Input.InputScopeNameValue.Url) } },
        };
        var homeStatus = new TextBlock { Style = Caption(), Visibility = Visibility.Collapsed };
        var homeSave = new Button { Content = Strings.Get("DialogSave") };
        homeSave.Click += (_, _) =>
        {
            var text = homeBox.Text.Trim();
            if (text.Length == 0)
            {
                _settings.HomePage = BrowserDefaults.HomeUrl;
                homeBox.Text = _settings.HomePage;
                ShowStatus(homeStatus, "SettingsHomePageSaved", error: false);
            }
            else if (AddressResolver.TryResolveAddress(text, out var uri) && BrowserSettings.IsValidHomePage(uri.AbsoluteUri))
            {
                _settings.HomePage = uri.AbsoluteUri;
                homeBox.Text = _settings.HomePage;
                ShowStatus(homeStatus, "SettingsHomePageSaved", error: false);
            }
            else
            {
                ShowStatus(homeStatus, "SettingsHomePageInvalid", error: true);
            }
        };
        var homeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        homeRow.Children.Add(homeBox);
        homeRow.Children.Add(homeSave);
        var homeControls = new StackPanel { Spacing = 4 };
        homeControls.Children.Add(homeRow);
        homeControls.Children.Add(homeStatus);
        panel.Children.Add(Card("SettingsHomePage", "SettingsHomePageDescription", homeControls, stacked: true));

        // Search engine.
        var engines = new ComboBox { MinWidth = 200 };
        foreach (var engine in BrowserSettings.SearchEngines)
        {
            engines.Items.Add(Strings.Get(engine.NameKey));
        }
        engines.SelectedIndex = IndexOf(BrowserSettings.SearchEngines, _settings.SearchEngine);
        engines.SelectionChanged += (_, _) =>
        {
            if (engines.SelectedIndex >= 0)
            {
                _settings.SearchEngine = BrowserSettings.SearchEngines[engines.SelectedIndex];
            }
        };
        panel.Children.Add(Card("SettingsSearchEngine", "SettingsSearchEngineDescription", engines));

        // Restore tabs.
        panel.Children.Add(Card("SettingsRestoreTabs", "SettingsRestoreTabsDescription",
            Toggle(_settings.RestoreTabs, on => _settings.RestoreTabs = on)));

        return panel;
    }

    // ---- নিরাপত্তা (Security) ----

    private StackPanel BuildSecurity()
    {
        var panel = NewPanel();
        panel.Children.Add(Card("SettingsSmartScreen", "SettingsSmartScreenDescription",
            Toggle(_settings.SmartScreen, on => _settings.SmartScreen = on)));
        panel.Children.Add(Card("SettingsPopups", "SettingsPopupsDescription", null));
        panel.Children.Add(Card("SettingsSafeSchemes", "SettingsSafeSchemesDescription", null));
        return panel;
    }

    // ---- গোপনীয়তা (Privacy) ----

    private StackPanel BuildPrivacy()
    {
        var panel = NewPanel();

        var levels = new RadioButtons();
        levels.Items.Add(Strings.Get("SettingsTrackingBasic"));
        levels.Items.Add(Strings.Get("SettingsTrackingBalanced"));
        levels.Items.Add(Strings.Get("SettingsTrackingStrict"));
        levels.SelectedIndex = (int)_settings.TrackingProtection;
        levels.SelectionChanged += (_, _) =>
        {
            if (levels.SelectedIndex >= 0)
            {
                _settings.TrackingProtection = (TrackingProtection)levels.SelectedIndex;
            }
        };
        panel.Children.Add(Card("SettingsTracking", "SettingsTrackingDescription", levels, stacked: true));

        var clearStatus = new TextBlock { Style = Caption(), Visibility = Visibility.Collapsed };
        var clearButton = new Button { Content = Strings.Get("SettingsClearSiteDataButton") };
        clearButton.Click += async (_, _) =>
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = Strings.Get("SettingsClearSiteDataConfirmTitle"),
                Content = Strings.Get("SettingsClearSiteDataConfirmMessage"),
                PrimaryButtonText = Strings.Get("HistoryConfirmYes"),
                CloseButtonText = Strings.Get("DialogCancel"),
                DefaultButton = ContentDialogButton.Close,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
            var cleared = await _clearSiteData();
            ShowStatus(clearStatus, cleared ? "SettingsClearSiteDataDone" : "SettingsClearSiteDataNothing", error: false);
        };
        var clearControls = new StackPanel { Spacing = 4 };
        clearControls.Children.Add(clearButton);
        clearControls.Children.Add(clearStatus);
        panel.Children.Add(Card("SettingsClearSiteData", "SettingsClearSiteDataDescription", clearControls, stacked: true));

        var historyLink = new HyperlinkButton { Content = Strings.Get("SettingsOpenHistory"), Padding = new Thickness(0) };
        historyLink.Click += (_, _) => _openHistory();
        panel.Children.Add(Card("SettingsHistory", "SettingsHistoryDescription", historyLink, stacked: true));
        return panel;
    }

    // ---- চেহারা (Appearance) ----

    private StackPanel BuildAppearance()
    {
        var panel = NewPanel();

        var themes = new RadioButtons();
        themes.Items.Add(Strings.Get("SettingsThemeSystem"));
        themes.Items.Add(Strings.Get("SettingsThemeLight"));
        themes.Items.Add(Strings.Get("SettingsThemeDark"));
        themes.SelectedIndex = (int)_settings.Theme;
        themes.SelectionChanged += (_, _) =>
        {
            if (themes.SelectedIndex >= 0)
            {
                _settings.Theme = (AppTheme)themes.SelectedIndex;
            }
        };
        panel.Children.Add(Card("SettingsTheme", "SettingsThemeDescription", themes, stacked: true));

        _bookmarkBarToggle = Toggle(_settings.ShowBookmarkBar, on => _settings.ShowBookmarkBar = on);
        panel.Children.Add(Card("SettingsBookmarkBar", "SettingsBookmarkBarDescription", _bookmarkBarToggle));

        _verticalTabsToggle = Toggle(_settings.VerticalTabs, on => _settings.VerticalTabs = on);
        panel.Children.Add(Card("SettingsVerticalTabs", "SettingsVerticalTabsDescription", _verticalTabsToggle));

        panel.Children.Add(Card("SettingsFont", "SettingsFontDescription", null));
        return panel;
    }

    // ---- উন্নত (Advanced) ----

    private StackPanel BuildAdvanced()
    {
        var panel = NewPanel();

        var openFolder = new Button { Content = Strings.Get("SettingsOpenDataFolder") };
        openFolder.Click += (_, _) =>
        {
            Directory.CreateDirectory(AppPaths.DataRoot);
            Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { AppPaths.DataRoot } });
        };
        var folderControls = new StackPanel { Spacing = 8 };
        folderControls.Children.Add(new TextBlock { Text = AppPaths.DataRoot, Style = Caption(), IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap });
        folderControls.Children.Add(openFolder);
        panel.Children.Add(Card("SettingsDataFolder", "SettingsDataFolderDescription", folderControls, stacked: true));

        var version = typeof(App).Assembly.GetName().Version ?? new Version(0, 0, 0);
        var versionText = Formatting.Digits($"{version.Major}.{version.Minor}.{version.Build}");
        panel.Children.Add(Card("SettingsVersion", null, new TextBlock { Text = versionText, VerticalAlignment = VerticalAlignment.Center }));
        return panel;
    }

    // ---- Building blocks ----

    private static StackPanel NewPanel() => new() { Spacing = 8 };

    /// <summary>A settings card: title and description, with the control on the right or underneath.</summary>
    private static Border Card(string titleKey, string? descriptionKey, FrameworkElement? control, bool stacked = false)
    {
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = Strings.Get(titleKey), TextWrapping = TextWrapping.Wrap });
        if (descriptionKey is not null)
        {
            text.Children.Add(new TextBlock
            {
                Text = Strings.Get(descriptionKey),
                Style = AppStyle("SecondaryCaptionTextBlockStyle"),
                TextWrapping = TextWrapping.Wrap,
            });
        }

        var grid = new Grid { ColumnSpacing = 16, RowSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(text);

        if (control is not null)
        {
            if (stacked)
            {
                Grid.SetRow(control, 1);
                Grid.SetColumnSpan(control, 2);
                control.HorizontalAlignment = HorizontalAlignment.Left;
            }
            else
            {
                Grid.SetColumn(control, 1);
                control.VerticalAlignment = VerticalAlignment.Center;
            }
            grid.Children.Add(control);
        }

        return new Border { Child = grid, Style = AppStyle("SettingsCardStyle") };
    }

    private ToggleSwitch Toggle(bool isOn, Action<bool> changed)
    {
        var toggle = new ToggleSwitch
        {
            IsOn = isOn,
            OnContent = Strings.Get("ToggleOn"),
            OffContent = Strings.Get("ToggleOff"),
            MinWidth = 0,
        };
        toggle.Toggled += (_, _) =>
        {
            if (!_refreshing)
            {
                changed(toggle.IsOn);
            }
        };
        return toggle;
    }

    private static Style Caption() => AppStyle("CaptionTextBlockStyle");

    private static Style AppStyle(string key) => (Style)Application.Current.Resources[key];

    private static void ShowStatus(TextBlock status, string key, bool error)
    {
        status.Text = Strings.Get(key);
        status.Style = AppStyle(error ? "CriticalCaptionTextBlockStyle" : "SuccessCaptionTextBlockStyle");
        status.Visibility = Visibility.Visible;
    }

    private static int IndexOf(IReadOnlyList<SearchEngine> engines, SearchEngine engine)
    {
        for (var i = 0; i < engines.Count; i++)
        {
            if (engines[i].Id == engine.Id)
            {
                return i;
            }
        }
        return 0;
    }
}
