namespace Obhijatri.Core.Storage;

public sealed record HistoryEntry(long Id, string Url, string Title, DateTimeOffset VisitedAt);

/// <summary>A frequently visited site: its name (the host without "www.") and its front page address.</summary>
public sealed record TopSite(string Host, string Url);

/// <summary>Browsing history. Private windows never write here.</summary>
public sealed class HistoryStore
{
    public const int MaxTitleLength = 500;
    public const int MaxUrlLength = 4096;

    private readonly BrowserDatabase _db;
    private readonly TimeProvider _time;

    public HistoryStore(BrowserDatabase db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Records a visit. Only http and https pages are stored.</summary>
    public long? AddVisit(string url, string? title)
    {
        if (!IsRecordable(url))
        {
            return null;
        }

        using var command = _db.Command("""
            INSERT INTO history (url, title, visited_at) VALUES ($url, $title, $at);
            SELECT last_insert_rowid();
            """);
        command.Parameters.AddWithValue("$url", url);
        command.Parameters.AddWithValue("$title", Truncate(title ?? string.Empty, MaxTitleLength));
        command.Parameters.AddWithValue("$at", _time.GetUtcNow().ToUnixTimeMilliseconds());
        return (long)command.ExecuteScalar()!;
    }

    /// <summary>Sets the title on the most recent visit to <paramref name="url"/>.</summary>
    public void UpdateLatestTitle(string url, string title)
    {
        using var command = _db.Command("""
            UPDATE history SET title = $title
            WHERE id = (SELECT id FROM history WHERE url = $url ORDER BY visited_at DESC, id DESC LIMIT 1);
            """);
        command.Parameters.AddWithValue("$url", url);
        command.Parameters.AddWithValue("$title", Truncate(title, MaxTitleLength));
        command.ExecuteNonQuery();
    }

    /// <summary>Newest first. An empty query returns the most recent visits.</summary>
    public IReadOnlyList<HistoryEntry> Search(string? query, int limit = 500)
    {
        var text = query?.Trim() ?? string.Empty;
        using var command = _db.Command(text.Length == 0
            ? "SELECT id, url, title, visited_at FROM history ORDER BY visited_at DESC, id DESC LIMIT $limit;"
            : """
              SELECT id, url, title, visited_at FROM history
              WHERE title LIKE $pattern ESCAPE '\' OR url LIKE $pattern ESCAPE '\'
              ORDER BY visited_at DESC, id DESC LIMIT $limit;
              """);
        command.Parameters.AddWithValue("$limit", limit);
        if (text.Length > 0)
        {
            command.Parameters.AddWithValue("$pattern", "%" + EscapeLike(text) + "%");
        }

        var results = new List<HistoryEntry>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new HistoryEntry(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(3))));
        }
        return results;
    }

    /// <summary>
    /// The sites visited most often in the last <paramref name="days"/> days, most visited first, as
    /// the site's front page. Only ordinary web addresses count.
    /// </summary>
    public IReadOnlyList<TopSite> TopSites(int limit, int days = 60)
    {
        var since = _time.GetUtcNow().AddDays(-days).ToUnixTimeMilliseconds();
        using var command = _db.Command("""
            SELECT url, COUNT(*) AS visits FROM history
            WHERE visited_at >= $since AND (url LIKE 'https://%' OR url LIKE 'http://%')
            GROUP BY url ORDER BY visits DESC LIMIT 500;
            """);
        command.Parameters.AddWithValue("$since", since);

        var perHost = new Dictionary<string, (string Url, long Visits)>(StringComparer.OrdinalIgnoreCase);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (!Uri.TryCreate(reader.GetString(0), UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
            {
                continue;
            }
            var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
            var visits = reader.GetInt64(1);
            perHost[host] = perHost.TryGetValue(host, out var known)
                ? (known.Url, known.Visits + visits)
                : (uri.GetLeftPart(UriPartial.Authority) + "/", visits);
        }

        return perHost
            .OrderByDescending(p => p.Value.Visits)
            .ThenBy(p => p.Key, StringComparer.Ordinal)
            .Take(limit)
            .Select(p => new TopSite(p.Key, p.Value.Url))
            .ToList();
    }

    public int Count() => Convert.ToInt32(_db.Scalar("SELECT COUNT(*) FROM history;"), System.Globalization.CultureInfo.InvariantCulture);

    public void Delete(long id)
    {
        using var command = _db.Command("DELETE FROM history WHERE id = $id;");
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    /// <summary>Deletes visits from the last <paramref name="span"/> (for example one hour).</summary>
    public int ClearLast(TimeSpan span)
    {
        using var command = _db.Command("DELETE FROM history WHERE visited_at >= $since;");
        command.Parameters.AddWithValue("$since", (_time.GetUtcNow() - span).ToUnixTimeMilliseconds());
        return command.ExecuteNonQuery();
    }

    public int ClearAll() => _db.Execute("DELETE FROM history;");

    internal static bool IsRecordable(string url) =>
        url.Length <= MaxUrlLength
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("%", "\\%", StringComparison.Ordinal)
             .Replace("_", "\\_", StringComparison.Ordinal);
}
