using System.Text.Json;

namespace Obhijatri.AI.Scoring;

/// <summary>What one form on the page asks for, and where it sends the answers.</summary>
/// <param name="ActionHost">The host the form submits to, or null when it submits to the page itself.</param>
public sealed record FormSignals(string? ActionHost, bool HasPassword, bool HasOtpOrPin, bool HasCard, bool HasPhone, bool HasIdNumber)
{
    public bool AsksSensitive => HasPassword || HasOtpOrPin || HasCard;

    public bool CollectsPersonalData => AsksSensitive || HasPhone || HasIdNumber;
}

/// <summary>What the page shows and asks for, as collected by the page script (untrusted).</summary>
/// <param name="HasContactLink">
/// The page links to a private chat with a number or channel (WhatsApp to a phone number, a Telegram
/// channel): scams that have no form ask people to "message us" instead. Share buttons do not count.
/// </param>
public sealed record PageSignals(string Url, string Title, string Text, IReadOnlyList<FormSignals> Forms, bool HasCountdown, bool HasContactLink = false)
{
    public const int MaxTextLength = 30_000;
    public const int MaxTitleLength = 300;
    public const int MaxForms = 20;
    public const int MaxHostLength = 253;

    /// <summary>A page whose content is unknown (not loaded, or our own warning page): only the address is judged.</summary>
    public static PageSignals AddressOnly(string url) => new(url, string.Empty, string.Empty, [], false);

    /// <summary>
    /// Reads the collecting script's JSON. The page's own scripts run beside ours, so the result is
    /// treated as untrusted: wrong shapes are ignored and every size is capped. Never throws.
    /// </summary>
    public static PageSignals Parse(string url, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return AddressOnly(url);
        }

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return AddressOnly(url);
            }

            var forms = new List<FormSignals>();
            if (root.TryGetProperty("forms", out var array) && array.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in array.EnumerateArray())
                {
                    if (forms.Count >= MaxForms)
                    {
                        break;
                    }
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        forms.Add(new FormSignals(
                            CleanHost(ReadString(item, "actionHost")),
                            ReadBool(item, "password"), ReadBool(item, "otp"), ReadBool(item, "card"),
                            ReadBool(item, "phone"), ReadBool(item, "id")));
                    }
                }
            }

            return new PageSignals(
                url,
                Truncate(ReadString(root, "title"), MaxTitleLength),
                Truncate(ReadString(root, "text"), MaxTextLength),
                forms,
                ReadBool(root, "countdown"),
                ReadBool(root, "contactLink"));
        }
        catch (JsonException)
        {
            return AddressOnly(url);
        }
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool ReadBool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string Truncate(string? text, int max) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Length <= max ? text : text[..max];

    /// <summary>Only a plausible host name is kept, so odd text cannot reach the scorer or the reasons.</summary>
    private static string? CleanHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Length > MaxHostLength)
        {
            return null;
        }
        var trimmed = host.Trim().ToLowerInvariant();
        return trimmed.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or ':' or '[' or ']') ? trimmed : null;
    }
}

/// <summary>What the scam shield already knows about the address, from local data only.</summary>
public sealed record ShieldFacts(bool IsKnownScam, bool IsSafeBrowsingFlagged, string? LookalikeBrandKey, string? LookalikeRealDomain)
{
    public static readonly ShieldFacts None = new(false, false, null, null);

    public bool IsLookalike => LookalikeBrandKey is not null;
}
