namespace Obhijatri.Safety.LowData;

/// <summary>
/// Embedded players and social widgets that pull in megabytes of video and script. In low data
/// mode a page still loads, but these frames are replaced by a short notice. Only frames that sit
/// inside another site's page are ever checked, so opening youtube.com itself is not affected.
/// </summary>
public static class HeavyEmbeds
{
    // Host suffix, and a path fragment when the host also serves ordinary pages (facebook.com).
    private static readonly (string Host, string? PathContains)[] Rules =
    [
        ("youtube.com", "/embed"),
        ("youtube-nocookie.com", null),
        ("player.vimeo.com", null),
        ("dailymotion.com", "/embed"),
        ("facebook.com", "/plugins/"),
        ("platform.twitter.com", null),
        ("syndication.twitter.com", null),
        ("platform.x.com", null),
        ("instagram.com", "/embed"),
        ("tiktok.com", "/embed"),
        ("player.twitch.tv", null),
        ("w.soundcloud.com", null),
        ("open.spotify.com", "/embed"),
    ];

    public static bool IsHeavyEmbed(string host, string path)
    {
        foreach (var (ruleHost, pathContains) in Rules)
        {
            if (!MatchesHost(host, ruleHost))
            {
                continue;
            }
            if (pathContains is null || path.Contains(pathContains, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool MatchesHost(string host, string ruleHost) =>
        host.Equals(ruleHost, StringComparison.OrdinalIgnoreCase)
        || (host.Length > ruleHost.Length
            && host.EndsWith(ruleHost, StringComparison.OrdinalIgnoreCase)
            && host[host.Length - ruleHost.Length - 1] == '.');
}
