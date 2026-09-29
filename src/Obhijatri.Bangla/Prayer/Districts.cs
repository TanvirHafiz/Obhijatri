using System.Reflection;
using System.Text.Json;

namespace Obhijatri.Bangla.Prayer;

/// <summary>A district of Bangladesh (its headquarters town) with names in both languages.</summary>
public sealed record District(string Id, string Bn, string En, double Lat, double Lon);

/// <summary>The 64 districts, for prayer times. Coordinates are the district headquarters, rounded to about 100 m.</summary>
public static class Districts
{
    public const string DefaultId = "dhaka";

    /// <summary>Bangladesh Standard Time, UTC+6, with no daylight saving.</summary>
    public const double UtcOffsetHours = 6;

    private static readonly Lazy<IReadOnlyList<District>> AllLazy = new(Load);

    public static IReadOnlyList<District> All => AllLazy.Value;

    public static bool IsValidId(string? id) => id is not null && All.Any(d => d.Id == id);

    public static District Get(string? id) => All.FirstOrDefault(d => d.Id == id) ?? All.First(d => d.Id == DefaultId);

    private static IReadOnlyList<District> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Obhijatri.Bangla.Districts.json")
                           ?? throw new InvalidOperationException("Missing districts data");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<List<District>>(stream, options) ?? [];
    }
}
