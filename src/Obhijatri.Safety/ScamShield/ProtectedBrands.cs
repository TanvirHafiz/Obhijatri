namespace Obhijatri.Safety.ScamShield;

/// <summary>One protected brand: its real registrable domain and a Bangla display name for warnings.</summary>
public sealed record ProtectedBrand(string Domain, string NameKey);

/// <summary>
/// Brands and services impersonated in BD scam pages: mobile financial services, banks, government
/// portals, and the global services (Facebook, Google, PayPal, ...) named in plan.md section 5,
/// Milestone 6. The "token" used to detect a brand name used in the wrong domain is the registrable
/// domain's own first label (for example "bracbank", not the shorter "brac"), so an unrelated site
/// that merely shares a short syllable is not flagged.
/// </summary>
public static class ProtectedBrands
{
    public static readonly IReadOnlyList<ProtectedBrand> All =
    [
        new("bkash.com", "BrandBkash"),
        new("nagad.com.bd", "BrandNagad"),
        new("rocket.com.bd", "BrandRocket"),
        new("dutchbanglabank.com", "BrandDutchBangla"),
        new("sonalibank.com.bd", "BrandSonaliBank"),
        new("agranibank.org", "BrandAgraniBank"),
        new("jb.com.bd", "BrandJanataBank"),
        new("rupalibank.org", "BrandRupaliBank"),
        new("islamibankbd.com", "BrandIslamiBank"),
        new("bracbank.com", "BrandBracBank"),
        new("thecitybank.com", "BrandCityBank"),
        new("ebl.com.bd", "BrandEbl"),
        new("bb.org.bd", "BrandBangladeshBank"),
        new("nidw.gov.bd", "BrandNid"),
        new("epassport.gov.bd", "BrandPassport"),
        new("bangladesh.gov.bd", "BrandGovBd"),
        new("brta.gov.bd", "BrandBrta"),
        new("grameenphone.com", "BrandGrameenphone"),
        new("robi.com.bd", "BrandRobi"),
        new("banglalink.net", "BrandBanglalink"),
        new("daraz.com.bd", "BrandDaraz"),
        new("facebook.com", "BrandFacebook"),
        new("whatsapp.com", "BrandWhatsapp"),
        new("instagram.com", "BrandInstagram"),
        new("google.com", "BrandGoogle"),
        new("microsoft.com", "BrandMicrosoft"),
        new("apple.com", "BrandApple"),
        new("amazon.com", "BrandAmazon"),
        new("paypal.com", "BrandPaypal"),
        new("netflix.com", "BrandNetflix"),
    ];

    /// <summary>The first label of the registrable domain, used as the brand's name token (lowercase).</summary>
    public static string Token(ProtectedBrand brand)
    {
        var dot = brand.Domain.IndexOf('.');
        return (dot < 0 ? brand.Domain : brand.Domain[..dot]).ToLowerInvariant();
    }
}
