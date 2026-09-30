using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using Obhijatri.App.Services;
using Obhijatri.Core.Settings;
using Windows.System;

namespace Obhijatri.App;

public sealed partial class MainWindow
{
    private void InitializeSettings()
    {
        AppServices.Settings.Changed += Settings_Changed;
        ApplyTheme();

        // Ctrl+, opens settings. Added here because XAML has no name for the comma key.
        var settingsKey = new KeyboardAccelerator { Key = (VirtualKey)188, Modifiers = VirtualKeyModifiers.Control };
        settingsKey.Invoked += (_, args) =>
        {
            args.Handled = true;
            OpenSettings();
        };
        Root.KeyboardAccelerators.Add(settingsKey);
    }

    /// <summary>Settings are shared by all windows; each window applies changes to itself.</summary>
    private void Settings_Changed(object? sender, string key)
    {
        switch (key)
        {
            case BrowserSettings.Keys.ShowBookmarkBar:
                ApplyBookmarkBarVisibility();
                break;
            case BrowserSettings.Keys.VerticalTabs:
                ApplyTabLayout();
                break;
            case BrowserSettings.Keys.Theme:
                ApplyTheme();
                ApplyUserSettingsToTabs();
                break;
            case BrowserSettings.Keys.AddressBarPhonetic:
                ResetPhonetic();
                UpdatePhoneticButton();
                break;
            case BrowserSettings.Keys.SmartScreen:
            case BrowserSettings.Keys.TrackingProtection:
                ApplyUserSettingsToTabs();
                break;
            case BrowserSettings.Keys.BlockAds:
                UpdateShieldButton();
                break;
            case BrowserSettings.Keys.GoogleTranslateEnabled:
            case BrowserSettings.Keys.OllamaEnabled:
                UpdateTranslateMenu();
                break;
            case BrowserSettings.Keys.ShowMemoryMeter:
                RamButton.Visibility = AppServices.Settings.ShowMemoryMeter ? Visibility.Visible : Visibility.Collapsed;
                break;
            case BrowserSettings.Keys.FixBanglaFonts:
                ApplyUserSettingsToTabs();
                break;
            case BrowserSettings.Keys.LowDataMode:
                MenuLowData.IsChecked = AppServices.Settings.LowDataMode;
                ApplyUserSettingsToTabs();
                break;
        }
    }

    private void ApplyUserSettingsToTabs()
    {
        foreach (var tab in _tabs)
        {
            tab.ApplyUserSettings();
        }
    }

    private void ApplyTheme()
    {
        var theme = AppServices.Settings.Theme;
        Root.RequestedTheme = theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        // Caption buttons (minimise, maximise, close) follow the same theme.
        AppWindow.TitleBar.PreferredTheme = theme switch
        {
            AppTheme.Light => TitleBarTheme.Light,
            AppTheme.Dark => TitleBarTheme.Dark,
            _ => TitleBarTheme.UseDefaultAppMode,
        };
    }

    /// <summary>
    /// Deletes cookies, site storage and the cache of the normal profile, and the per-site choices
    /// (Bangla typing). Returns false when there was nothing to clear.
    /// </summary>
    private async Task<bool> ClearSiteDataAsync()
    {
        var clearedChoices = AppServices.SitePreferences.ClearAll() > 0;
        App.NotifySitePhonetic(null, false, isPrivate: false);

        if (AppServices.NormalProfile is not { } profile)
        {
            return clearedChoices;
        }

        await profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllSite | CoreWebView2BrowsingDataKinds.DiskCache);
        return true;
    }

    // ---- Per-site Bangla typing (ITabHost) ----

    private readonly Dictionary<string, bool> _privatePhonetic = new(StringComparer.OrdinalIgnoreCase);

    public bool GetSitePhonetic(string host) =>
        IsPrivate ? _privatePhonetic.GetValueOrDefault(host) : AppServices.SitePreferences.GetPhonetic(host);

    /// <summary>Private windows keep the choice in memory only; nothing is written to disk.</summary>
    public void SetSitePhonetic(string host, bool enabled)
    {
        if (IsPrivate)
        {
            _privatePhonetic[host] = enabled;
            NotifySitePhonetic(host, enabled);
        }
        else
        {
            AppServices.SitePreferences.SetPhonetic(host, enabled);
            App.NotifySitePhonetic(host, enabled, isPrivate: false);
        }
    }

    internal void NotifySitePhonetic(string? host, bool enabled)
    {
        foreach (var tab in _tabs)
        {
            tab.NotifySitePhonetic(host, enabled);
        }
    }
}
