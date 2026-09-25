using Microsoft.Web.WebView2.Core;
using Obhijatri.Core;

namespace Obhijatri.App.Services;

/// <summary>One WebView2 environment (one browser process) shared by every tab and window.</summary>
internal static class WebViewEnvironment
{
    public const string PrivateProfileName = "Private";

    private static Task<CoreWebView2Environment>? _environment;

    public static Task<CoreWebView2Environment> GetAsync() => _environment ??= CreateAsync();

    private static async Task<CoreWebView2Environment> CreateAsync()
    {
        Directory.CreateDirectory(AppPaths.WebViewData);
        var options = new CoreWebView2EnvironmentOptions
        {
            // Bangla for the engine's own context menus, dialogs and error pages.
            Language = AppServices.Settings.EngineLanguage,
        };
        return await CoreWebView2Environment.CreateWithOptionsAsync(string.Empty, AppPaths.WebViewData, options);
    }
}
