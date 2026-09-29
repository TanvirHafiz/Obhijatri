namespace Obhijatri.Core;

/// <summary>Addresses of the browser's own pages. They are shown in the app, never loaded from the web.</summary>
public static class InternalPages
{
    public const string Scheme = "obhijatri";
    public const string History = "obhijatri://history";
    public const string Settings = "obhijatri://settings";
    public const string NewTab = "obhijatri://newtab";

    public static bool IsInternal(string? address) =>
        address is not null && address.StartsWith(Scheme + "://", StringComparison.OrdinalIgnoreCase);
}
