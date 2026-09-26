namespace Obhijatri.Core.Storage;

/// <summary>
/// Per-site choices, keyed by host name. Only sites where Bangla phonetic typing is switched on
/// are stored, so the table does not become a list of every site visited.
/// </summary>
public sealed class SitePreferencesStore
{
    private readonly BrowserDatabase _db;

    public SitePreferencesStore(BrowserDatabase db)
    {
        _db = db;
    }

    public bool GetPhonetic(string host)
    {
        using var command = _db.Command("SELECT phonetic FROM site_preferences WHERE host = $host;");
        command.Parameters.AddWithValue("$host", Normalize(host));
        return command.ExecuteScalar() is long value && value == 1;
    }

    public void SetPhonetic(string host, bool enabled)
    {
        var key = Normalize(host);
        if (key.Length == 0)
        {
            return;
        }

        using var command = _db.Command(enabled
            ? "INSERT INTO site_preferences (host, phonetic) VALUES ($host, 1) ON CONFLICT(host) DO UPDATE SET phonetic = 1;"
            : "DELETE FROM site_preferences WHERE host = $host;");
        command.Parameters.AddWithValue("$host", key);
        command.ExecuteNonQuery();
    }

    public int Count() => Convert.ToInt32(_db.Scalar("SELECT COUNT(*) FROM site_preferences;"), System.Globalization.CultureInfo.InvariantCulture);

    public int ClearAll() => _db.Execute("DELETE FROM site_preferences;");

    private static string Normalize(string host) => host.Trim().TrimEnd('.').ToLowerInvariant();
}
