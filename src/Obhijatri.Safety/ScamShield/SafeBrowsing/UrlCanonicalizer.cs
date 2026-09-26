using System.Net;
using System.Text;

namespace Obhijatri.Safety.ScamShield.SafeBrowsing;

/// <summary>
/// Builds the URL forms the Google Safe Browsing v4 API expects to be hashed: a canonical form of
/// the URL (https://developers.google.com/safe-browsing/v4/urls-hashing), then every combination of
/// a host suffix and a path prefix that the API defines as a "URL expression".
/// </summary>
public static class UrlCanonicalizer
{
    /// <summary>Every URL expression for <paramref name="uri"/>, most specific first.</summary>
    public static IReadOnlyList<string> Expressions(Uri uri)
    {
        var host = CanonicalHost(uri.Host);
        var path = CanonicalPath(uri);

        var hosts = HostSuffixes(host);
        var paths = PathPrefixes(path);

        var expressions = new List<string>();
        foreach (var h in hosts)
        {
            foreach (var p in paths)
            {
                expressions.Add(h + p);
            }
        }
        return expressions;
    }

    private static string CanonicalHost(string host)
    {
        var h = host.Trim().TrimEnd('.').ToLowerInvariant();
        while (h.Contains("..", StringComparison.Ordinal))
        {
            h = h.Replace("..", ".", StringComparison.Ordinal);
        }
        return h;
    }

    private static string CanonicalPath(Uri uri)
    {
        var path = Unescape(uri.AbsolutePath);
        if (path.Length == 0)
        {
            path = "/";
        }
        var query = Unescape(uri.Query);
        return path + query;
    }

    /// <summary>Percent-decodes repeatedly, as the spec requires, so double-encoded tricks are caught.</summary>
    private static string Unescape(string value)
    {
        var current = value;
        for (var i = 0; i < 5; i++)
        {
            var next = WebUtility.UrlDecode(current);
            if (next == current)
            {
                break;
            }
            current = next;
        }
        return current;
    }

    /// <summary>
    /// The full host, then up to 4 more suffixes formed by dropping leading labels, stopping once
    /// only two labels remain (a bare "com" is never checked on its own).
    /// </summary>
    private static List<string> HostSuffixes(string host)
    {
        var result = new List<string> { host };
        if (System.Net.IPAddress.TryParse(host, out _))
        {
            return result;
        }

        var labels = host.Split('.');
        var maxExtra = Math.Min(4, labels.Length - 2);
        for (var drop = 1; drop <= maxExtra; drop++)
        {
            result.Add(string.Join('.', labels[drop..]));
        }
        return result;
    }

    /// <summary>At most 6 path prefixes: the full path and query, then up to 4 leading-segment prefixes, then "/".</summary>
    private static List<string> PathPrefixes(string path)
    {
        var result = new List<string> { path };
        var withoutQuery = path.Split('?')[0];
        var segments = withoutQuery.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var built = "";
        for (var i = 0; i < segments.Length - 1 && result.Count < 5; i++)
        {
            built += "/" + segments[i];
            result.Add(built + "/");
        }
        if (!result.Contains("/"))
        {
            result.Add("/");
        }
        return result;
    }

    public static byte[] Sha256(string expression) => System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(expression));
}
