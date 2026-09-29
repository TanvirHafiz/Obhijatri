using Microsoft.Web.WebView2.Core;
using Obhijatri.Safety;

namespace Obhijatri.App.Services;

/// <summary>
/// Cookie auto-delete (Milestone 7, opt-in): when the app is closing, cookies for sites the user has
/// not bookmarked are removed from the normal (non-private) profile. Runs once, right before the app
/// exits, so it never gets in the way of a browsing session.
/// </summary>
internal static class CookieAutoDelete
{
    public static async Task RunAsync(CoreWebView2Profile profile)
    {
        var keep = new HashSet<string>(StringComparer.Ordinal);
        foreach (var url in AppServices.Bookmarks.GetAllUrls())
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                keep.Add(RegistrableDomain.Get(uri.Host));
            }
        }

        var manager = profile.CookieManager;
        var cookies = await manager.GetCookiesAsync(null);
        foreach (var cookie in cookies)
        {
            if (!keep.Contains(RegistrableDomain.Get(cookie.Domain)))
            {
                manager.DeleteCookie(cookie);
            }
        }
    }
}
