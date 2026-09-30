namespace Obhijatri.AI.Scoring;

/// <summary>The traffic light shown to the user. Always an estimate, never a guarantee.</summary>
public enum ScamLevel
{
    Green,
    Yellow,
    Red,
}

/// <summary>One kind of warning sign. The app turns each into a plain Bangla sentence.</summary>
public enum ScamSignal
{
    KnownScamSite,
    SafeBrowsingFlag,
    LookalikeDomain,
    ImpersonatesBrand,
    BrandOnWrongDomain,
    PinOrOtpRequest,
    CardDetailsRequest,
    FormSendsElsewhere,
    UrgencyWords,
    PrizeClaim,
    SimOrKycThreat,
    JobOrMoneyOffer,
    CountdownTimer,
    SuspiciousAddress,
    NoHttps,

    /// <summary>Not a warning: the address is the real site of a protected brand.</summary>
    OfficialSite,
}

/// <param name="Signal">What was found.</param>
/// <param name="Weight">How much it counted towards the score.</param>
/// <param name="BrandNameKey">The resource key of the brand it concerns (Signal-dependent), for the sentence.</param>
/// <param name="RealDomain">For a lookalike: the real site's address.</param>
public sealed record ScamReason(ScamSignal Signal, int Weight, string? BrandNameKey = null, string? RealDomain = null);

/// <summary>The rule based verdict for one page: a level, the score and the reasons, strongest first.</summary>
public sealed record ScamAssessment(ScamLevel Level, int Score, IReadOnlyList<ScamReason> Reasons)
{
    public const int TopReasonCount = 3;

    /// <summary>The three strongest reasons, which is what the user is shown.</summary>
    public IReadOnlyList<ScamReason> TopReasons => Reasons.Take(TopReasonCount).ToList();
}
