namespace Obhijatri.Core.Storage;

public sealed record SessionTab(string Url, string Title, bool IsActive, bool IsPinned = false);

/// <summary>The open tabs of the normal (non-private) window, restored on the next start.</summary>
public sealed class SessionStore
{
    private readonly BrowserDatabase _db;

    public SessionStore(BrowserDatabase db)
    {
        _db = db;
    }

    public void Save(IReadOnlyList<SessionTab> tabs)
    {
        using var transaction = _db.Connection.BeginTransaction();
        _db.Execute("DELETE FROM session_tabs;", transaction);
        for (var i = 0; i < tabs.Count; i++)
        {
            using var command = _db.Command(
                "INSERT INTO session_tabs (position, url, title, is_active, is_pinned) VALUES ($pos, $url, $title, $active, $pinned);",
                transaction);
            command.Parameters.AddWithValue("$pos", i);
            command.Parameters.AddWithValue("$url", tabs[i].Url);
            command.Parameters.AddWithValue("$title", tabs[i].Title);
            command.Parameters.AddWithValue("$active", tabs[i].IsActive ? 1 : 0);
            command.Parameters.AddWithValue("$pinned", tabs[i].IsPinned ? 1 : 0);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public IReadOnlyList<SessionTab> Load()
    {
        using var command = _db.Command("SELECT url, title, is_active, is_pinned FROM session_tabs ORDER BY position;");
        using var reader = command.ExecuteReader();
        var tabs = new List<SessionTab>();
        while (reader.Read())
        {
            tabs.Add(new SessionTab(reader.GetString(0), reader.GetString(1), reader.GetInt64(2) == 1, reader.GetInt64(3) == 1));
        }
        return tabs;
    }
}
