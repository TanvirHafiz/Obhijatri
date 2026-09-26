namespace Obhijatri.Core.Storage;

/// <summary>
/// Per-site choices, keyed by host name: Bangla phonetic typing on, ads allowed. A row exists only
/// while at least one choice is switched on, so the table does not become a list of every site visited.
/// </summary>
public sealed class SitePreferencesStore
{
    private readonly BrowserDatabase _db;

    public SitePreferencesStore(BrowserDatabase db)
    {
        _db = db;
    }

    public bool GetPhonetic(string host) => Get(host, "phonetic");

    public void SetPhonetic(string host, bool enabled) => Set(host, "phonetic", enabled);

    public bool GetAdsAllowed(string host) => Get(host, "ads_allowed");

    public void SetAdsAllowed(string host, bool allowed) => Set(host, "ads_allowed", allowed);

    public int Count() => Convert.ToInt32(_db.Scalar("SELECT COUNT(*) FROM site_preferences;"), System.Globalization.CultureInfo.InvariantCulture);

    public int ClearAll() => _db.Execute("DELETE FROM site_preferences;");

    // Column names come only from the constants above, never from input.
    private bool Get(string host, string column)
    {
        using var command = _db.Command($"SELECT {column} FROM site_preferences WHERE host = $host;");
        command.Parameters.AddWithValue("$host", Normalize(host));
        return command.ExecuteScalar() is long value && value == 1;
    }

    private void Set(string host, string column, bool enabled)
    {
        var key = Normalize(host);
        if (key.Length == 0)
        {
            return;
        }

        using var transaction = _db.Connection.BeginTransaction();
        using (var upsert = _db.Command($"""
            INSERT INTO site_preferences (host, phonetic, ads_allowed) VALUES ($host, 0, 0) ON CONFLICT(host) DO NOTHING;
            UPDATE site_preferences SET {column} = $value WHERE host = $host;
            DELETE FROM site_preferences WHERE host = $host AND phonetic = 0 AND ads_allowed = 0;
            """, transaction))
        {
            upsert.Parameters.AddWithValue("$host", key);
            upsert.Parameters.AddWithValue("$value", enabled ? 1 : 0);
            upsert.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    private static string Normalize(string host) => host.Trim().TrimEnd('.').ToLowerInvariant();
}
