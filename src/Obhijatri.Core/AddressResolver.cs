using System.Net;
using System.Text.RegularExpressions;

namespace Obhijatri.Core;

/// <summary>
/// Turns whatever the user typed into the address bar into a URI to navigate to:
/// either the address itself, or a search for the text.
/// </summary>
public static partial class AddressResolver
{
    public const string GoogleSearchTemplate = "https://www.google.com/search?q={0}";

    // Only these schemes may be opened from the address bar. Anything else
    // (javascript:, data:, file:, vbscript:, ms-* protocol handlers) is treated as search text.
    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        Uri.UriSchemeHttp,
        Uri.UriSchemeHttps,
    };

    public static Uri? Resolve(string? input, string searchTemplate = GoogleSearchTemplate)
    {
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri("about:blank");
        }

        if (!text.Any(char.IsWhiteSpace))
        {
            if (ExplicitScheme().IsMatch(text))
            {
                if (Uri.TryCreate(text, UriKind.Absolute, out var absolute)
                    && AllowedSchemes.Contains(absolute.Scheme)
                    && !string.IsNullOrEmpty(absolute.Host))
                {
                    return absolute;
                }
            }
            else if (LooksLikeHost(text, out var isLocal)
                && Uri.TryCreate((isLocal ? "http://" : "https://") + text, UriKind.Absolute, out var guessed))
            {
                return guessed;
            }
        }

        return BuildSearch(text, searchTemplate);
    }

    public static Uri BuildSearch(string query, string searchTemplate = GoogleSearchTemplate) =>
        new(string.Format(System.Globalization.CultureInfo.InvariantCulture, searchTemplate, Uri.EscapeDataString(query)));

    private static bool LooksLikeHost(string text, out bool isLocal)
    {
        isLocal = false;
        var end = text.IndexOfAny(['/', '?', '#']);
        var hostAndPort = end < 0 ? text : text[..end];

        // Strip an optional :port.
        var host = hostAndPort;
        var colon = hostAndPort.LastIndexOf(':');
        if (colon > 0 && !hostAndPort.StartsWith('['))
        {
            var port = hostAndPort[(colon + 1)..];
            if (port.Length == 0 || !port.All(char.IsAsciiDigit) || !int.TryParse(port, out var p) || p > 65535)
            {
                return false;
            }
            host = hostAndPort[..colon];
        }

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            isLocal = true;
            return true;
        }

        if (IPAddress.TryParse(host.Trim('[', ']'), out var ip))
        {
            isLocal = IPAddress.IsLoopback(ip);
            // Require a dotted quad or bracketed IPv6 so plain numbers like "1234" stay searches.
            return host.Count(c => c == '.') == 3 || host.StartsWith('[');
        }

        if (Uri.CheckHostName(host) != UriHostNameType.Dns)
        {
            return false;
        }

        var lastDot = host.LastIndexOf('.');
        if (lastDot <= 0 || lastDot == host.Length - 1)
        {
            return false;
        }

        // Top level domain: letters only (or an IDN "xn--" label), at least two characters.
        var tld = host[(lastDot + 1)..];
        return tld.Length >= 2
            && (tld.All(char.IsLetter) || tld.StartsWith("xn--", StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex("^[a-zA-Z][a-zA-Z0-9+.-]*:(?!\\d+(/|$))")]
    private static partial Regex ExplicitScheme();
}
