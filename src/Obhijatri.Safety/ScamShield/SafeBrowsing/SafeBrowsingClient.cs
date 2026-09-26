using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Obhijatri.Safety.ScamShield.SafeBrowsing;

/// <summary>
/// Google Safe Browsing v4 Update API client. Only 4-byte hash prefixes of the threat lists are ever
/// downloaded and stored; a navigation is checked against those local prefixes, so no address the
/// user visits is ever sent anywhere. If no API key is configured this is skipped entirely (every
/// lookup returns false, and no request is made).
///
/// v1 scope: full updates only (no partial-update diffing or checksum verification), and the local
/// prefix hit itself is treated as the signal for the warning page, without a further
/// fullHashes:find network round trip. A 4-byte prefix collision could in theory warn on a clean
/// address; this only matters when an API key is configured, which this build has not been tested
/// against (see PROGRESS.md).
/// </summary>
public sealed class SafeBrowsingClient
{
    private const string UpdateUrl = "https://safebrowsing.googleapis.com/v4/threatListUpdates:fetch";
    private static readonly string[] ThreatTypes = ["MALWARE", "SOCIAL_ENGINEERING", "UNWANTED_SOFTWARE"];

    private readonly string _prefixFilePath;
    private HashSet<string> _prefixes = new(StringComparer.Ordinal);

    public SafeBrowsingClient(string prefixFilePath)
    {
        _prefixFilePath = prefixFilePath;
        Load();
    }

    public int PrefixCount => _prefixes.Count;

    /// <summary>True when <paramref name="uri"/> matches a locally cached threat hash prefix.</summary>
    public bool IsFlagged(Uri uri)
    {
        if (_prefixes.Count == 0)
        {
            return false;
        }
        foreach (var expression in UrlCanonicalizer.Expressions(uri))
        {
            var hash = UrlCanonicalizer.Sha256(expression);
            var prefix = Convert.ToBase64String(hash, 0, 4);
            if (_prefixes.Contains(prefix))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Downloads the current threat list prefixes. Does nothing (and never contacts Google) when
    /// <paramref name="apiKey"/> is empty. Returns true if the local cache changed.
    /// </summary>
    public async Task<bool> UpdateAsync(HttpClient http, string apiKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return false;
        }

        var request = new UpdateRequest(
            new ClientInfo("obhijatri-browser", "1"),
            ThreatTypes.Select(t => new ListUpdateRequest(t, "ANY_PLATFORM", "URL",
                new Constraints(["RAW"], 100_000, null, null))).ToArray());

        using var content = new StringContent(
            JsonSerializer.Serialize(request, SafeBrowsingJson.Default.UpdateRequest),
            Encoding.UTF8, "application/json");

        using var response = await http.PostAsync($"{UpdateUrl}?key={Uri.EscapeDataString(apiKey)}", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var parsed = JsonSerializer.Deserialize(body, SafeBrowsingJson.Default.UpdateResponse);
        if (parsed?.ListUpdateResponses is not { Length: > 0 } lists)
        {
            return false;
        }

        var prefixes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var list in lists)
        {
            foreach (var addition in list.Additions ?? [])
            {
                if (addition.RawHashes is not { PrefixSize: 4, RawHashesBase64: { Length: > 0 } raw })
                {
                    continue; // only the simple, uncompressed form is supported in v1
                }
                var bytes = Convert.FromBase64String(raw);
                for (var i = 0; i + 4 <= bytes.Length; i += 4)
                {
                    prefixes.Add(Convert.ToBase64String(bytes, i, 4));
                }
            }
        }

        _prefixes = prefixes;
        await File.WriteAllLinesAsync(_prefixFilePath, prefixes, cancellationToken);
        return true;
    }

    private void Load()
    {
        if (!File.Exists(_prefixFilePath))
        {
            return;
        }
        try
        {
            _prefixes = new HashSet<string>(File.ReadAllLines(_prefixFilePath), StringComparer.Ordinal);
        }
        catch (IOException)
        {
            _prefixes = new HashSet<string>(StringComparer.Ordinal);
        }
    }
}

internal sealed record ClientInfo(
    [property: JsonPropertyName("clientId")] string ClientId,
    [property: JsonPropertyName("clientVersion")] string ClientVersion);

internal sealed record Constraints(
    [property: JsonPropertyName("supportedCompressions")] string[] SupportedCompressions,
    [property: JsonPropertyName("maxDatabaseEntries")] int MaxDatabaseEntries,
    [property: JsonPropertyName("region")] string? Region,
    [property: JsonPropertyName("language")] string? Language);

internal sealed record ListUpdateRequest(
    [property: JsonPropertyName("threatType")] string ThreatType,
    [property: JsonPropertyName("platformType")] string PlatformType,
    [property: JsonPropertyName("threatEntryType")] string ThreatEntryType,
    [property: JsonPropertyName("constraints")] Constraints Constraints,
    [property: JsonPropertyName("state")] string State = "");

internal sealed record UpdateRequest(
    [property: JsonPropertyName("client")] ClientInfo Client,
    [property: JsonPropertyName("listUpdateRequests")] ListUpdateRequest[] ListUpdateRequests);

internal sealed record RawHashes(
    [property: JsonPropertyName("prefixSize")] int PrefixSize,
    [property: JsonPropertyName("rawHashes")] string RawHashesBase64);

internal sealed record Addition(
    [property: JsonPropertyName("rawHashes")] RawHashes? RawHashes);

internal sealed record ListUpdateResponse(
    [property: JsonPropertyName("threatType")] string? ThreatType,
    [property: JsonPropertyName("additions")] Addition[]? Additions,
    [property: JsonPropertyName("newClientState")] string? NewClientState,
    [property: JsonPropertyName("responseType")] string? ResponseType);

internal sealed record UpdateResponse(
    [property: JsonPropertyName("listUpdateResponses")] ListUpdateResponse[]? ListUpdateResponses);

[JsonSerializable(typeof(UpdateRequest))]
[JsonSerializable(typeof(UpdateResponse))]
internal sealed partial class SafeBrowsingJson : JsonSerializerContext;
