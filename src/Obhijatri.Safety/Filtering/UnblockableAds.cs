namespace Obhijatri.Safety.Filtering;

/// <summary>
/// Sites whose main ads come from the site's own servers (video ads, sponsored posts), so a request
/// filter cannot remove them. The shield must say so instead of implying they were blocked.
/// </summary>
public static class UnblockableAds
{
    private static readonly (string Domain, string Name)[] Sites =
    [
        ("youtube.com", "YouTube"),
        ("youtu.be", "YouTube"),
        ("facebook.com", "Facebook"),
        ("fb.com", "Facebook"),
        ("instagram.com", "Instagram"),
    ];

    /// <summary>The site's display name if its ads cannot be blocked here, otherwise null.</summary>
    public static string? NameFor(string? host)
    {
        if (string.IsNullOrEmpty(host))
        {
            return null;
        }
        foreach (var (domain, name) in Sites)
        {
            if (host.Equals(domain, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }
        return null;
    }
}
