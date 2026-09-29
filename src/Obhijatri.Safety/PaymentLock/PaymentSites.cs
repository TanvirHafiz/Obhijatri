namespace Obhijatri.Safety.PaymentLock;

/// <summary>
/// Banking and mobile-financial-services domains that turn on payment lock mode: third-party
/// scripts are blocked outright (not just ones on the ad/tracker lists) and the toolbar shows a
/// green "safe mode" badge. Reuses the same registrable domains as the scam shield's protected
/// brand list (Obhijatri.Safety.ScamShield.ProtectedBrands), restricted to the ones that are
/// actually banks or payment services rather than social or search sites.
/// </summary>
public static class PaymentSites
{
    public static readonly IReadOnlyList<string> Domains =
    [
        "bkash.com",
        "nagad.com.bd",
        "rocket.com.bd",
        "dutchbanglabank.com",
        "sonalibank.com.bd",
        "agranibank.org",
        "jb.com.bd",
        "rupalibank.org",
        "islamibankbd.com",
        "bracbank.com",
        "thecitybank.com",
        "ebl.com.bd",
        "bb.org.bd",
        "paypal.com",
    ];

    public static bool IsPaymentSite(string host) => Domains.Contains(RegistrableDomain.Get(host), StringComparer.Ordinal);
}
