using Microsoft.Windows.ApplicationModel.Resources;
using Obhijatri.App.Services;

namespace Obhijatri.App.Localization;

/// <summary>
/// Reads user-facing text from Strings/*/Resources.resw. The UI language is chosen
/// explicitly (Bangla by default, from settings) instead of following the Windows display language.
/// AppServices must be initialized before the first lookup.
/// </summary>
internal static class Strings
{
    private static readonly ResourceManager Manager = new();
    private static readonly ResourceMap Map = Manager.MainResourceMap.GetSubtree("Resources");
    private static readonly ResourceContext Context = CreateContext();

    public static string Get(string key)
    {
        var candidate = Map.TryGetValue(key, Context);
        return candidate?.ValueAsString ?? key;
    }

    public static string Format(string key, params object[] args) =>
        string.Format(System.Globalization.CultureInfo.CurrentCulture, Get(key), args);

    private static ResourceContext CreateContext()
    {
        var context = Manager.CreateResourceContext();
        context.QualifierValues["Language"] = AppServices.Settings.UiLanguage;
        return context;
    }
}
