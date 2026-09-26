namespace Obhijatri.Safety.ScamShield;

public enum ScamReasonKind
{
    /// <summary>The domain looks like a protected brand (punycode, homoglyph or typo trick).</summary>
    Lookalike,

    /// <summary>The domain is on the local BD scam list.</summary>
    KnownScam,

    /// <summary>Google Safe Browsing flagged the address.</summary>
    SafeBrowsing,
}

/// <summary>
/// The result of the scam shield's check for one navigation. <see cref="BrandNameKey"/> and
/// <see cref="RealDomain"/> are set only for <see cref="ScamReasonKind.Lookalike"/>, so the warning
/// page can name the real site.
/// </summary>
public sealed record ScamVerdict(ScamReasonKind Reason, string? BrandNameKey = null, string? RealDomain = null)
{
    public static ScamVerdict Lookalike(ProtectedBrand brand) => new(ScamReasonKind.Lookalike, brand.NameKey, brand.Domain);

    public static readonly ScamVerdict KnownScam = new(ScamReasonKind.KnownScam);

    public static readonly ScamVerdict SafeBrowsing = new(ScamReasonKind.SafeBrowsing);
}
