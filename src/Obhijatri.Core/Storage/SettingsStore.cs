using System.Globalization;

namespace Obhijatri.Core.Storage;

/// <summary>Simple key/value settings. Keys live in <see cref="SettingKeys"/>.</summary>
public sealed class SettingsStore
{
    private readonly BrowserDatabase _db;

    public SettingsStore(BrowserDatabase db)
    {
        _db = db;
    }

    public string? GetString(string key)
    {
        using var command = _db.Command("SELECT value FROM settings WHERE key = $key;");
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }

    public void SetString(string key, string value)
    {
        using var command = _db.Command("""
            INSERT INTO settings (key, value) VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """);
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    public bool GetBool(string key, bool defaultValue) =>
        bool.TryParse(GetString(key), out var value) ? value : defaultValue;

    public void SetBool(string key, bool value) => SetString(key, value.ToString(CultureInfo.InvariantCulture));
}

public static class SettingKeys
{
    public const string VerticalTabs = "tabs.vertical";
    public const string ShowBookmarkBar = "bookmarks.showBar";
}
