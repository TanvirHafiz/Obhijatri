using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Obhijatri.App.Localization;

/// <summary>
/// Sets the app's UI font. The Bangla UI uses the bundled Hind Siliguri (with Noto Sans Bengali as a
/// fallback for rare characters); the English UI keeps Segoe UI and uses the bundled fonts for any
/// Bangla text such as page titles.
/// </summary>
internal static class AppFonts
{
    private const string Folder = "ms-appx:///Assets/Fonts/";
    private const string Noto = Folder + "NotoSansBengali-Variable.ttf#Noto Sans Bengali";
    private const string HindRegular = Folder + "HindSiliguri-Regular.ttf#Hind Siliguri";
    private const string HindSemiBold = Folder + "HindSiliguri-SemiBold.ttf#Hind Siliguri";
    private const string SystemFonts = "Segoe UI Variable Text, Segoe UI, Nirmala UI";

    /// <summary>Text styles that are drawn semi-bold and should use the real semi-bold font file.</summary>
    private static readonly string[] SemiBoldStyles =
    [
        "TitleTextBlockStyle",
        "SubtitleTextBlockStyle",
        "BodyStrongTextBlockStyle",
        "TitleLargeTextBlockStyle",
        "DisplayTextBlockStyle",
        "PhoneticOnTextStyle",
    ];

    private static readonly string[] RegularStyles =
    [
        "CaptionTextBlockStyle",
        "BodyTextBlockStyle",
        "SecondaryCaptionTextBlockStyle",
        "SuccessCaptionTextBlockStyle",
        "CriticalCaptionTextBlockStyle",
        "PhoneticOffTextStyle",
    ];

    public static void Apply(ResourceDictionary resources, bool bangla)
    {
        var regular = new FontFamily(bangla
            ? $"{HindRegular}, {Noto}, {SystemFonts}"
            : $"{SystemFonts}, {HindRegular}, {Noto}");
        var semiBold = new FontFamily(bangla
            ? $"{HindSemiBold}, {Noto}, {SystemFonts}"
            : $"{SystemFonts}, {HindSemiBold}, {Noto}");

        // Controls (buttons, menus, tabs, tooltips, text boxes) read this theme resource.
        resources["ContentControlThemeFontFamily"] = regular;

        // Plain TextBlocks without a style.
        var textBlock = new Style(typeof(TextBlock));
        textBlock.Setters.Add(new Setter(TextBlock.FontFamilyProperty, regular));
        resources[typeof(TextBlock)] = textBlock;

        foreach (var key in SemiBoldStyles)
        {
            OverrideStyleFont(resources, key, semiBold);
        }
        foreach (var key in RegularStyles)
        {
            OverrideStyleFont(resources, key, regular);
        }
    }

    /// <summary>Replaces a built-in text style with one based on it that only changes the font.</summary>
    private static void OverrideStyleFont(ResourceDictionary resources, string key, FontFamily font)
    {
        if (resources.TryGetValue(key, out var existing) && existing is Style baseStyle)
        {
            var style = new Style(typeof(TextBlock)) { BasedOn = baseStyle };
            style.Setters.Add(new Setter(TextBlock.FontFamilyProperty, font));
            resources[key] = style;
        }
    }
}
