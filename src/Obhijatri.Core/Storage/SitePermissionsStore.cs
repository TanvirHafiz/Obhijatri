namespace Obhijatri.Core.Storage;

public enum SitePermissionKind
{
    Camera,
    Microphone,
    Location,
    Notifications,
}

public enum SitePermissionState
{
    Ask = 0,
    Allow = 1,
    Deny = 2,
}

public sealed record SitePermissionRow(string Host, SitePermissionState Camera, SitePermissionState Microphone,
    SitePermissionState Location, SitePermissionState Notifications)
{
    public bool HasAnyDecision => Camera != SitePermissionState.Ask || Microphone != SitePermissionState.Ask
        || Location != SitePermissionState.Ask || Notifications != SitePermissionState.Ask;
}

/// <summary>
/// Per-site camera, microphone, location and notification choices, keyed by host name. A row exists
/// only while at least one permission is away from "ask" (the default), the same pattern as
/// <see cref="SitePreferencesStore"/>. Backs the permission dashboard in Settings.
/// </summary>
public sealed class SitePermissionsStore
{
    private readonly BrowserDatabase _db;

    public SitePermissionsStore(BrowserDatabase db)
    {
        _db = db;
    }

    public SitePermissionState Get(string host, SitePermissionKind kind)
    {
        using var command = _db.Command($"SELECT {Column(kind)} FROM site_permissions WHERE host = $host;");
        command.Parameters.AddWithValue("$host", Normalize(host));
        return command.ExecuteScalar() is long value ? (SitePermissionState)value : SitePermissionState.Ask;
    }

    public void Set(string host, SitePermissionKind kind, SitePermissionState state)
    {
        var key = Normalize(host);
        if (key.Length == 0)
        {
            return;
        }

        var column = Column(kind);
        using var transaction = _db.Connection.BeginTransaction();
        using (var upsert = _db.Command($"""
            INSERT INTO site_permissions (host, camera, microphone, location, notifications) VALUES ($host, 0, 0, 0, 0) ON CONFLICT(host) DO NOTHING;
            UPDATE site_permissions SET {column} = $value WHERE host = $host;
            DELETE FROM site_permissions WHERE host = $host AND camera = 0 AND microphone = 0 AND location = 0 AND notifications = 0;
            """, transaction))
        {
            upsert.Parameters.AddWithValue("$host", key);
            upsert.Parameters.AddWithValue("$value", (int)state);
            upsert.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    /// <summary>Every site with at least one permission away from "ask", for the dashboard.</summary>
    public IReadOnlyList<SitePermissionRow> ListSitesWithDecisions()
    {
        var rows = new List<SitePermissionRow>();
        using var command = _db.Command("SELECT host, camera, microphone, location, notifications FROM site_permissions ORDER BY host;");
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new SitePermissionRow(
                reader.GetString(0),
                (SitePermissionState)reader.GetInt64(1),
                (SitePermissionState)reader.GetInt64(2),
                (SitePermissionState)reader.GetInt64(3),
                (SitePermissionState)reader.GetInt64(4)));
        }
        return rows;
    }

    public int ClearAll() => _db.Execute("DELETE FROM site_permissions;");

    private static string Column(SitePermissionKind kind) => kind switch
    {
        SitePermissionKind.Camera => "camera",
        SitePermissionKind.Microphone => "microphone",
        SitePermissionKind.Location => "location",
        SitePermissionKind.Notifications => "notifications",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string Normalize(string host) => host.Trim().TrimEnd('.').ToLowerInvariant();
}
