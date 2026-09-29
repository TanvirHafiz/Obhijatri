using System.Globalization;
using Obhijatri.App.Services;
using Obhijatri.Bangla;

namespace Obhijatri.App.Localization;

/// <summary>
/// Numbers, sizes and dates for display. In the Bangla UI numbers use Bangla digits and
/// lakh grouping (১২,৩৪,৫৬৭); in English they use ASCII digits and thousands grouping.
/// </summary>
internal static class Formatting
{
    private static bool IsBangla => AppServices.Settings.IsBangla;

    public static string Number(long value) =>
        BanglaNumerals.Format(value, IsBangla ? NumberGrouping.Lakh : NumberGrouping.Thousands, banglaDigits: IsBangla);

    public static string Decimal(double value, int decimals) =>
        BanglaNumerals.Format((decimal)value, decimals, IsBangla ? NumberGrouping.Lakh : NumberGrouping.Thousands, banglaDigits: IsBangla);

    /// <summary>Digits inside any text (for example a version number) in the UI's digit style.</summary>
    public static string Digits(string text) => IsBangla ? BanglaNumerals.ToBanglaDigits(text) : text;

    public static string Bytes(long bytes)
    {
        const double Kb = 1024, Mb = Kb * 1024, Gb = Mb * 1024;
        return bytes switch
        {
            < 1024 => Strings.Format("SizeBytesFormat", Number(bytes)),
            < 1024 * 1024 => Strings.Format("SizeKbFormat", Decimal(bytes / Kb, 0)),
            < 1024L * 1024 * 1024 => Strings.Format("SizeMbFormat", Decimal(bytes / Mb, 1)),
            _ => Strings.Format("SizeGbFormat", Decimal(bytes / Gb, 2)),
        };
    }

    /// <summary>For example "২৫ সেপ্টেম্বর ২০২৬, বিকাল ৫:১৮" or "25 Sep 2026, 5:18 PM".</summary>
    public static string DateTime(DateTimeOffset value)
    {
        var local = value.ToLocalTime();
        var month = Strings.Get("Month" + local.Month.ToString(CultureInfo.InvariantCulture));
        return Strings.Format("DateTimeFormat", Number(local.Day), month, Digits(local.Year.ToString(CultureInfo.InvariantCulture)), Time(local));
    }

    private static string Time(DateTimeOffset local) => Time(local.Hour, local.Minute);

    /// <summary>A time of day, for example "ভোর ৪:৩৪" style text as set by the TimeFormat string.</summary>
    public static string Time(TimeOnly time) => Time(time.Hour, time.Minute);

    private static string Time(int hour, int minute)
    {
        var hour12 = hour % 12 == 0 ? 12 : hour % 12;
        var clock = Digits($"{hour12.ToString(CultureInfo.InvariantCulture)}:{minute.ToString("00", CultureInfo.InvariantCulture)}");
        return Strings.Format("TimeFormat", clock, Strings.Get(PeriodKey(hour)));
    }

    /// <summary>
    /// Bangla names the part of the day (ভোর, সকাল, দুপুর, বিকাল, সন্ধ্যা, রাত);
    /// English uses AM and PM.
    /// </summary>
    private static string PeriodKey(int hour)
    {
        if (!IsBangla)
        {
            return hour < 12 ? "TimeAm" : "TimePm";
        }

        return hour switch
        {
            >= 4 and < 6 => "TimeDawn",
            >= 6 and < 12 => "TimeMorning",
            >= 12 and < 15 => "TimeNoon",
            >= 15 and < 18 => "TimeAfternoon",
            >= 18 and < 20 => "TimeEvening",
            _ => "TimeNight",
        };
    }
}
