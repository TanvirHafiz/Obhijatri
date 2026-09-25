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
            case BrowserSettings.Keys.SmartScreen:
            case BrowserSettings.Keys.TrackingProtection:
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
    /// Deletes cookies, site storage and the cache of the normal profile. Returns false when no web
    /// page has been opened yet this run (there is then no engine profile to clear).
    /// </summary>
    private async Task<bool> ClearSiteDataAsync()
    {
        if (AppServices.NormalProfile is not { } profile)
        {
            return false;
        }

        await profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllSite | CoreWebView2BrowsingDataKinds.DiskCache);
        return true;
    }
}
