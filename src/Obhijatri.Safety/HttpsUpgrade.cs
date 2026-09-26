using System.Net;
using System.Net.Sockets;

namespace Obhijatri.Safety;

/// <summary>
/// HTTPS-only mode: which http:// addresses are switched to https://. Local and private addresses
/// (localhost, home routers such as 192.168.0.1, single-word intranet names, .local and .test) are
/// left alone, because they almost never have a valid certificate.
/// </summary>
public static class HttpsUpgrade
{
    private static readonly string[] LocalSuffixes = [".localhost", ".local", ".test", ".internal", ".home.arpa", ".lan"];

    public static bool ShouldUpgrade(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttp && !IsLocalOrPrivate(uri.Host);

    /// <summary>The same address with https:// (and the default port if http used port 80).</summary>
    public static Uri ToHttps(Uri uri) =>
        new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = uri.IsDefaultPort ? -1 : uri.Port }.Uri;

    public static bool IsLocalOrPrivate(string host)
    {
        var h = host.Trim('[', ']').TrimEnd('.').ToLowerInvariant();
        if (h.Length == 0 || h == "localhost" || !h.Contains('.') && !h.Contains(':'))
        {
            return true;
        }
        if (LocalSuffixes.Any(s => h.EndsWith(s, StringComparison.Ordinal)))
        {
            return true;
        }
        if (!IPAddress.TryParse(h, out var ip))
        {
            return false;
        }
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal;
        }

        var b = ip.GetAddressBytes();
        return b[0] == 10
               || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
               || (b[0] == 192 && b[1] == 168)
               || (b[0] == 169 && b[1] == 254)
               || (b[0] == 100 && b[1] >= 64 && b[1] <= 127); // carrier-grade NAT (some mobile hotspots)
    }
}
