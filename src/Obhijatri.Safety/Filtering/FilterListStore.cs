using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Obhijatri.Safety.Filtering;

public sealed record FilterListSource(string Id, string Url, string ExpectedTitle);

public sealed record FilterListStatus(string Id, bool FromDownload, DateTimeOffset? DownloadedAt, string? Version);

/// <summary>
/// Keeps the filter lists on disk and updates them.
///  - A copy of each list ships with the app (gzip) so blocking works on first start and offline.
///  - Downloaded lists are saved with their SHA-256 and used only if the file still matches it, so a
///    damaged or edited file falls back to the bundled copy.
///  - A download is accepted only if it looks like the right list (header and title), is not
///    suspiciously small compared with the list in use, and is within a size limit.
/// Updates run in the background at most once a week. Only the list files are downloaded; nothing
/// about the user's browsing is sent.
/// </summary>
public sealed class FilterListStore
{
    public static readonly IReadOnlyList<FilterListSource> DefaultSources =
    [
        new("easylist", "https://easylist.to/easylist/easylist.txt", "EasyList"),
        new("easyprivacy", "https://easylist.to/easylist/easyprivacy.txt", "EasyPrivacy"),
    ];

    public static readonly TimeSpan UpdateInterval = TimeSpan.FromDays(7);
    private const long MaxDownloadBytes = 20 * 1024 * 1024;
    private const int MinLines = 1000;

    private readonly string _folder;
    private readonly string _bundledFolder;
    private readonly IReadOnlyList<FilterListSource> _sources;
    private readonly TimeProvider _time;

    public FilterListStore(string folder, string bundledFolder, IReadOnlyList<FilterListSource>? sources = null, TimeProvider? time = null)
    {
        _folder = folder;
        _bundledFolder = bundledFolder;
        _sources = sources ?? DefaultSources;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>All rule lines: each list (downloaded if valid, otherwise bundled) plus any extra bundled lists.</summary>
    public IEnumerable<string> ReadAllLines(IReadOnlyList<string>? extraBundledFiles = null)
    {
        foreach (var source in _sources)
        {
            foreach (var line in ReadList(source, out _))
            {
                yield return line;
            }
        }
        foreach (var extra in extraBundledFiles ?? [])
        {
            var path = Path.Combine(_bundledFolder, extra);
            if (File.Exists(path))
            {
                foreach (var line in File.ReadLines(path))
                {
                    yield return line;
                }
            }
        }
    }

    public IReadOnlyList<FilterListStatus> Status() => _sources.Select(source =>
    {
        var meta = ReadVerifiedMeta(source);
        return new FilterListStatus(source.Id, meta is not null, meta?.DownloadedAt, meta?.Version);
    }).ToList();

    /// <summary>When the oldest downloaded list was fetched, or null if any list has never been downloaded.</summary>
    public DateTimeOffset? LastUpdated
    {
        get
        {
            var status = Status();
            return status.All(s => s.DownloadedAt is not null) ? status.Min(s => s.DownloadedAt) : null;
        }
    }

    public bool IsUpdateDue() => LastUpdated is not { } last || _time.GetUtcNow() - last >= UpdateInterval;

    /// <summary>Downloads every list. Returns how many were updated. A bad download never replaces a good list.</summary>
    public async Task<int> UpdateAsync(HttpClient http, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_folder);
        var updated = 0;
        foreach (var source in _sources)
        {
            string text;
            try
            {
                using var response = await http.GetAsync(source.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxDownloadBytes)
                {
                    continue;
                }
                text = await ReadLimitedAsync(response.Content, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidDataException)
            {
                continue;
            }

            if (Accept(source, text))
            {
                Save(source, text);
                updated++;
            }
        }
        return updated;
    }

    /// <summary>Checks that a downloaded text really is the expected list and not an error page or a cut-off file.</summary>
    internal bool Accept(FilterListSource source, string text)
    {
        var hasTitle = text.Contains("! Title: " + source.ExpectedTitle + "\n", StringComparison.Ordinal)
                       || text.Contains("! Title: " + source.ExpectedTitle + "\r\n", StringComparison.Ordinal);
        if (!text.StartsWith("[Adblock Plus", StringComparison.Ordinal) || !hasTitle)
        {
            return false;
        }

        var newLines = CountLines(text);
        if (newLines < MinLines)
        {
            return false;
        }

        // Much smaller than the list in use suggests a truncated or tampered file.
        var currentLines = ReadList(source, out _).Count();
        return currentLines == 0 || newLines >= currentLines / 2;
    }

    private IEnumerable<string> ReadList(FilterListSource source, out bool fromDownload)
    {
        var path = DownloadedPath(source);
        if (ReadVerifiedMeta(source) is not null)
        {
            fromDownload = true;
            return File.ReadLines(path);
        }

        fromDownload = false;
        var bundled = Path.Combine(_bundledFolder, source.Id + ".txt.gz");
        return File.Exists(bundled) ? ReadGzipLines(bundled) : [];
    }

    private static IEnumerable<string> ReadGzipLines(string path)
    {
        using var file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    /// <summary>The metadata of the downloaded copy, only if the file's hash still matches it.</summary>
    private ListMeta? ReadVerifiedMeta(FilterListSource source)
    {
        var path = DownloadedPath(source);
        var metaPath = MetaPath(source);
        if (!File.Exists(path) || !File.Exists(metaPath))
        {
            return null;
        }

        try
        {
            var meta = JsonSerializer.Deserialize(File.ReadAllText(metaPath), ListMetaJson.Default.ListMeta);
            if (meta is null)
            {
                return null;
            }
            using var file = File.OpenRead(path);
            var hash = Convert.ToHexString(SHA256.HashData(file));
            return string.Equals(hash, meta.Sha256, StringComparison.OrdinalIgnoreCase) ? meta : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void Save(FilterListSource source, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var path = DownloadedPath(source);
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, path, overwrite: true);

        var meta = new ListMeta(Convert.ToHexString(SHA256.HashData(bytes)), _time.GetUtcNow(), ReadVersion(text));
        File.WriteAllText(MetaPath(source), JsonSerializer.Serialize(meta, ListMetaJson.Default.ListMeta));
    }

    private static async Task<string> ReadLimitedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxDownloadBytes)
            {
                throw new InvalidDataException("Filter list is too large.");
            }
        }
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static string? ReadVersion(string text)
    {
        const string Marker = "! Version: ";
        var start = text.IndexOf(Marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }
        start += Marker.Length;
        var end = text.IndexOfAny(['\r', '\n'], start);
        return end < 0 ? null : text[start..end].Trim();
    }

    private static int CountLines(string text) => text.Count(c => c == '\n');

    private string DownloadedPath(FilterListSource source) => Path.Combine(_folder, source.Id + ".txt");

    private string MetaPath(FilterListSource source) => Path.Combine(_folder, source.Id + ".json");
}

internal sealed record ListMeta(string Sha256, DateTimeOffset DownloadedAt, string? Version);

[System.Text.Json.Serialization.JsonSerializable(typeof(ListMeta))]
internal sealed partial class ListMetaJson : System.Text.Json.Serialization.JsonSerializerContext;
