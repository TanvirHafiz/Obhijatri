using System.Globalization;

namespace Obhijatri.App.Localization;

/// <summary>Number and size formatting. Bangla numerals are added in Milestone 3.</summary>
internal static class Formatting
{
    public static string Bytes(long bytes)
    {
        const double Kb = 1024, Mb = Kb * 1024, Gb = Mb * 1024;
        return bytes switch
        {
            < 1024 => Strings.Format("SizeBytesFormat", bytes.ToString(CultureInfo.InvariantCulture)),
            < 1024 * 1024 => Strings.Format("SizeKbFormat", (bytes / Kb).ToString("0", CultureInfo.InvariantCulture)),
            < 1024L * 1024 * 1024 => Strings.Format("SizeMbFormat", (bytes / Mb).ToString("0.0", CultureInfo.InvariantCulture)),
            _ => Strings.Format("SizeGbFormat", (bytes / Gb).ToString("0.00", CultureInfo.InvariantCulture)),
        };
    }

    public static string DateTime(DateTimeOffset value) =>
        value.ToLocalTime().ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture);
}
