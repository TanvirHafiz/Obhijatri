using Microsoft.Data.Sqlite;

namespace Obhijatri.Core.Storage;

public sealed record Bookmark(long Id, long? ParentId, bool IsFolder, string Title, string? Url, int Position);

/// <summary>
/// Bookmarks and folders. Items with no parent sit directly on the bookmark bar.
/// Deleting a folder deletes everything inside it.
/// </summary>
public sealed class BookmarkStore
{
    public const int MaxTitleLength = 500;

    private readonly BrowserDatabase _db;
    private readonly TimeProvider _time;

    public BookmarkStore(BrowserDatabase db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Raised after any change, so the bookmark bar can redraw.</summary>
    public event EventHandler? Changed;

    public long AddBookmark(long? parentId, string title, string url) =>
        AddBookmarkCore(parentId, title, url, null, raise: true);

    public long AddFolder(long? parentId, string title) =>
        Insert(parentId, isFolder: true, title, url: null, transaction: null, raise: true);

    public Bookmark? Get(long id)
    {
        using var command = _db.Command("SELECT id, parent_id, is_folder, title, url, position FROM bookmarks WHERE id = $id;");
        command.Parameters.AddWithValue("$id", id);
        return ReadAll(command).FirstOrDefault();
    }

    /// <summary>Children of a folder, or the bookmark bar when <paramref name="parentId"/> is null, in order.</summary>
    public IReadOnlyList<Bookmark> GetChildren(long? parentId)
    {
        using var command = _db.Command(parentId is null
            ? "SELECT id, parent_id, is_folder, title, url, position FROM bookmarks WHERE parent_id IS NULL ORDER BY position, id;"
            : "SELECT id, parent_id, is_folder, title, url, position FROM bookmarks WHERE parent_id = $parent ORDER BY position, id;");
        if (parentId is not null)
        {
            command.Parameters.AddWithValue("$parent", parentId.Value);
        }
        return ReadAll(command);
    }

    public IReadOnlyList<Bookmark> GetAllFolders()
    {
        using var command = _db.Command("SELECT id, parent_id, is_folder, title, url, position FROM bookmarks WHERE is_folder = 1 ORDER BY title;");
        return ReadAll(command);
    }

    /// <summary>Every bookmarked page's address (not folders). Used by cookie auto-delete.</summary>
    public IReadOnlyList<string> GetAllUrls()
    {
        var urls = new List<string>();
        using var command = _db.Command("SELECT url FROM bookmarks WHERE is_folder = 0 AND url IS NOT NULL;");
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            urls.Add(reader.GetString(0));
        }
        return urls;
    }

    public bool IsBookmarked(string url)
    {
        using var command = _db.Command("SELECT EXISTS(SELECT 1 FROM bookmarks WHERE url = $url);");
        command.Parameters.AddWithValue("$url", url);
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) == 1;
    }

    /// <summary>Removes every bookmark pointing at <paramref name="url"/>.</summary>
    public int RemoveByUrl(string url)
    {
        using var command = _db.Command("DELETE FROM bookmarks WHERE url = $url AND is_folder = 0;");
        command.Parameters.AddWithValue("$url", url);
        var removed = command.ExecuteNonQuery();
        if (removed > 0)
        {
            RaiseChanged();
        }
        return removed;
    }

    public void Rename(long id, string title)
    {
        using var command = _db.Command("UPDATE bookmarks SET title = $title WHERE id = $id;");
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$title", CleanTitle(title));
        command.ExecuteNonQuery();
        RaiseChanged();
    }

    public void Delete(long id)
    {
        using var command = _db.Command("DELETE FROM bookmarks WHERE id = $id;");
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
        RaiseChanged();
    }

    /// <summary>Moves an item into <paramref name="newParentId"/> at <paramref name="index"/> (0 based).</summary>
    public void Move(long id, long? newParentId, int index)
    {
        if (newParentId == id || (newParentId is long p && IsDescendant(p, id)))
        {
            throw new InvalidOperationException("A folder cannot be moved into itself.");
        }

        var siblings = GetChildren(newParentId).Where(b => b.Id != id).Select(b => b.Id).ToList();
        siblings.Insert(Math.Clamp(index, 0, siblings.Count), id);

        using var transaction = _db.Connection.BeginTransaction();
        using (var parent = _db.Command("UPDATE bookmarks SET parent_id = $parent WHERE id = $id;", transaction))
        {
            parent.Parameters.AddWithValue("$parent", (object?)newParentId ?? DBNull.Value);
            parent.Parameters.AddWithValue("$id", id);
            parent.ExecuteNonQuery();
        }

        for (var i = 0; i < siblings.Count; i++)
        {
            using var position = _db.Command("UPDATE bookmarks SET position = $pos WHERE id = $id;", transaction);
            position.Parameters.AddWithValue("$pos", i);
            position.Parameters.AddWithValue("$id", siblings[i]);
            position.ExecuteNonQuery();
        }

        transaction.Commit();
        RaiseChanged();
    }

    /// <summary>
    /// Imports a parsed bookmark tree into a new folder on the bookmark bar, in one transaction.
    /// Returns the number of bookmarks (not folders) added.
    /// </summary>
    public int Import(IReadOnlyList<Bookmarks.ImportedBookmark> items, string folderTitle)
    {
        using var transaction = _db.Connection.BeginTransaction();
        var root = Insert(null, isFolder: true, folderTitle, url: null, transaction, raise: false);
        var count = ImportInto(root, items, transaction);
        transaction.Commit();
        RaiseChanged();
        return count;
    }

    private int ImportInto(long parentId, IReadOnlyList<Bookmarks.ImportedBookmark> items, SqliteTransaction transaction)
    {
        var count = 0;
        foreach (var item in items)
        {
            if (item.IsFolder)
            {
                var folder = Insert(parentId, isFolder: true, item.Title, url: null, transaction, raise: false);
                count += ImportInto(folder, item.Children, transaction);
            }
            else if (item.Url is not null)
            {
                AddBookmarkCore(parentId, item.Title, item.Url, transaction, raise: false);
                count++;
            }
        }
        return count;
    }

    private long AddBookmarkCore(long? parentId, string title, string url, SqliteTransaction? transaction, bool raise)
    {
        if (!IsAllowedUrl(url))
        {
            throw new ArgumentException("Only http and https addresses can be bookmarked.", nameof(url));
        }
        return Insert(parentId, isFolder: false, title, url, transaction, raise);
    }

    private long Insert(long? parentId, bool isFolder, string title, string? url, SqliteTransaction? transaction, bool raise)
    {
        using var command = _db.Command("""
            INSERT INTO bookmarks (parent_id, is_folder, title, url, position, created_at)
            VALUES ($parent, $folder, $title, $url,
                    (SELECT COALESCE(MAX(position) + 1, 0) FROM bookmarks WHERE parent_id IS $parent),
                    $at);
            SELECT last_insert_rowid();
            """, transaction);
        command.Parameters.AddWithValue("$parent", (object?)parentId ?? DBNull.Value);
        command.Parameters.AddWithValue("$folder", isFolder ? 1 : 0);
        command.Parameters.AddWithValue("$title", CleanTitle(title));
        command.Parameters.AddWithValue("$url", (object?)url ?? DBNull.Value);
        command.Parameters.AddWithValue("$at", _time.GetUtcNow().ToUnixTimeMilliseconds());
        var id = (long)command.ExecuteScalar()!;
        if (raise)
        {
            RaiseChanged();
        }
        return id;
    }

    private bool IsDescendant(long candidate, long ancestor)
    {
        long? current = candidate;
        while (current is long id)
        {
            if (id == ancestor)
            {
                return true;
            }
            current = Get(id)?.ParentId;
        }
        return false;
    }

    internal static bool IsAllowedUrl(string url) =>
        url.Length <= HistoryStore.MaxUrlLength
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static string CleanTitle(string title)
    {
        var trimmed = title.Trim();
        return trimmed.Length <= MaxTitleLength ? trimmed : trimmed[..MaxTitleLength];
    }

    private static List<Bookmark> ReadAll(SqliteCommand command)
    {
        var list = new List<Bookmark>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Bookmark(
                reader.GetInt64(0),
                reader.IsDBNull(1) ? null : reader.GetInt64(1),
                reader.GetInt64(2) == 1,
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetInt32(5)));
        }
        return list;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
