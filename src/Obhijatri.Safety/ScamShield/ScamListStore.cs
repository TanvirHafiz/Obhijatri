using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Obhijatri.Safety.ScamShield;

/// <summary>The signed list as read from disk: domains, a version stamp, and the signature over them.</summary>
public sealed record ScamListDocument(string Version, IReadOnlyList<string> Domains, string SignatureBase64);

[JsonSerializable(typeof(ScamListDocument))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class ScamListJson : JsonSerializerContext;

/// <summary>
/// The BD scam domain list: a small signed JSON file. A copy ships with the app so the list works
/// offline from first start; an updated copy can be fetched once a day from a URL the owner
/// configures (empty by default, so nothing is fetched until one is set: plan.md section 9 leaves
/// "who hosts the list" as an open decision). A downloaded file is used only if its signature
/// verifies against the bundled public key, so a compromised or tampered host cannot add or remove
/// entries; a bad download leaves the previous (or bundled) list in place.
/// </summary>
public sealed class ScamListStore
{
    public static readonly TimeSpan UpdateInterval = TimeSpan.FromDays(1);
    private const long MaxDownloadBytes = 2 * 1024 * 1024;

    private readonly string _downloadedPath;
    private readonly string _bundledPath;
    private readonly ECDsa _publicKey;
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private HashSet<string> _domains = new(StringComparer.Ordinal);
    private string? _version;
    private DateTimeOffset? _loadedAt;

    public ScamListStore(string downloadedPath, string bundledPath, string publicKeyPem, TimeProvider? time = null)
    {
        _downloadedPath = downloadedPath;
        _bundledPath = bundledPath;
        _time = time ?? TimeProvider.System;
        _publicKey = ECDsa.Create();
        _publicKey.ImportFromPem(publicKeyPem);
        Load();
    }

    /// <summary>True when <paramref name="host"/>'s site is on the list.</summary>
    public bool IsListed(string host)
    {
        var registrable = RegistrableDomain.Get(host);
        lock (_gate)
        {
            return _domains.Contains(registrable);
        }
    }

    public string? Version
    {
        get { lock (_gate) { return _version; } }
    }

    public int Count
    {
        get { lock (_gate) { return _domains.Count; } }
    }

    public DateTimeOffset? LastUpdated
    {
        get { lock (_gate) { return File.Exists(_downloadedPath) ? _loadedAt : null; } }
    }

    public bool IsUpdateDue() => LastUpdated is not { } last || _time.GetUtcNow() - last >= UpdateInterval;

    /// <summary>Fetches the list from <paramref name="url"/>. Returns true if a new, verified list replaced the old one.</summary>
    public async Task<bool> UpdateAsync(HttpClient http, string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        string text;
        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxDownloadBytes)
            {
                return false;
            }
            text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (Encoding.UTF8.GetByteCount(text) > MaxDownloadBytes)
            {
                return false;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return false;
        }

        if (!TryVerify(text, out var document) || document is null)
        {
            return false;
        }

        var temp = _downloadedPath + ".tmp";
        await File.WriteAllTextAsync(temp, text, cancellationToken);
        File.Move(temp, _downloadedPath, overwrite: true);
        Load();
        return true;
    }

    /// <summary>Verifies the signature over the domain list. Public so tests can check bad signatures are rejected.</summary>
    public bool TryVerify(string json, out ScamListDocument? document)
    {
        document = null;
        try
        {
            var candidate = JsonSerializer.Deserialize(json, ScamListJson.Default.ScamListDocument);
            if (candidate is null)
            {
                return false;
            }
            var signed = SignedBytes(candidate.Version, candidate.Domains);
            var signature = Convert.FromBase64String(candidate.SignatureBase64);
            if (!_publicKey.VerifyData(signed, signature, HashAlgorithmName.SHA256))
            {
                return false;
            }
            document = candidate;
            return true;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or CryptographicException)
        {
            return false;
        }
    }

    /// <summary>The bytes the signature covers: the version and domains, in a fixed, unambiguous form.</summary>
    public static byte[] SignedBytes(string version, IReadOnlyList<string> domains) =>
        Encoding.UTF8.GetBytes(version + "\n" + string.Join("\n", domains));

    private void Load()
    {
        var path = File.Exists(_downloadedPath) ? _downloadedPath : _bundledPath;
        if (!File.Exists(path))
        {
            return;
        }

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (IOException)
        {
            return;
        }

        if (!TryVerify(text, out var document) || document is null)
        {
            // The downloaded copy failed verification (tampered or corrupt): fall back to the bundled one.
            if (path == _downloadedPath && File.Exists(_bundledPath))
            {
                path = _bundledPath;
                try
                {
                    text = File.ReadAllText(path);
                }
                catch (IOException)
                {
                    return;
                }
                if (!TryVerify(text, out document) || document is null)
                {
                    return;
                }
            }
            else
            {
                return;
            }
        }

        lock (_gate)
        {
            _domains = new HashSet<string>(document.Domains, StringComparer.Ordinal);
            _version = document.Version;
            _loadedAt = path == _downloadedPath
                ? File.GetLastWriteTimeUtc(path)
                : null;
        }
    }
}
