namespace Obhijatri.Safety;

/// <summary>
/// The "site" part of a host name (eTLD+1): www.prothomalo.com and images.prothomalo.com are the
/// same site, prothomalo.com. Without a full public suffix list this uses a rule that is right for
/// normal domains and for two-level country domains such as .com.bd, .gov.bd and .co.uk.
/// </summary>
public static class RegistrableDomain
{
    private static readonly HashSet<string> SecondLevelLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "com", "co", "org", "net", "gov", "edu", "ac", "mil", "info", "or", "ne", "go", "nic", "ltd", "plc", "sch",
    };

    public static string Get(string host)
    {
        var h = host.Trim().TrimEnd('.').ToLowerInvariant();
        if (h.Length == 0 || System.Net.IPAddress.TryParse(h.Trim('[', ']'), out _))
        {
            return h;
        }

        var labels = h.Split('.');
        if (labels.Length <= 2)
        {
            return h;
        }

        var last = labels[^1];
        var second = labels[^2];
        var take = last.Length == 2 && SecondLevelLabels.Contains(second) ? 3 : 2;
        return string.Join('.', labels[^Math.Min(take, labels.Length)..]);
    }

    /// <summary>True when the two hosts belong to different sites.</summary>
    public static bool IsThirdParty(string requestHost, string pageHost) =>
        pageHost.Length == 0 || !string.Equals(Get(requestHost), Get(pageHost), StringComparison.Ordinal);
}
