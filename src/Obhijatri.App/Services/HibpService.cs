using Obhijatri.Safety.Privacy;

namespace Obhijatri.App.Services;

/// <summary>Shared HTTP client and settings gate for the Have I Been Pwned password leak check.</summary>
internal static class HibpService
{
    private static readonly HibpClient Client = new();
    private static HttpClient? _http;

    /// <summary>
    /// Null if the check did not run at all (setting off, or the hash was not well-formed); 0 or
    /// more if it did (a network failure inside the check also comes back as null).
    /// </summary>
    public static async Task<int?> CheckAsync(string sha1Hex)
    {
        if (!AppServices.Settings.PasswordLeakCheckEnabled)
        {
            return null;
        }

        _http ??= new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await Client.CheckHashAsync(_http, sha1Hex, cancel.Token);
    }
}
