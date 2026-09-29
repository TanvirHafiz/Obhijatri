using System.Security.Cryptography;
using System.Text;

namespace Obhijatri.Safety.Privacy;

/// <summary>
/// Checks a password against Have I Been Pwned's range API using k-anonymity: only the first 5 hex
/// characters of the password's SHA-1 hash ever leave the PC. The full hash and the matching suffix
/// list from the response are compared locally, so HIBP never sees enough of the hash to identify
/// the password, and the app never learns anything about passwords other than this one.
/// </summary>
public sealed class HibpClient
{
    private const string RangeUrlFormat = "https://api.pwnedpasswords.com/range/{0}";

    /// <summary>The SHA-1 hex digest of <paramref name="password"/>, uppercase, for <see cref="CheckHashAsync"/>.</summary>
    public static string Sha1Hex(string password) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(password)));

    /// <summary>
    /// Returns how many times the password behind <paramref name="sha1Hex"/> (a 40 hex character
    /// SHA-1 digest, never the password itself) has appeared in a known breach, or 0 if not found.
    /// Returns null if the check could not be completed (network problem, unexpected response).
    /// </summary>
    public async Task<int?> CheckHashAsync(HttpClient http, string sha1Hex, CancellationToken cancellationToken)
    {
        if (sha1Hex.Length != 40 || !sha1Hex.All(Uri.IsHexDigit))
        {
            return null;
        }

        var prefix = sha1Hex[..5].ToUpperInvariant();
        var suffix = sha1Hex[5..].ToUpperInvariant();

        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(string.Format(RangeUrlFormat, prefix), cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            foreach (var rawLine in body.Split('\n'))
            {
                var line = rawLine.Trim();
                var colon = line.IndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }
                if (string.Equals(line[..colon], suffix, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(line[(colon + 1)..].Trim(), out var count))
                {
                    return count;
                }
            }
            return 0;
        }
    }
}
