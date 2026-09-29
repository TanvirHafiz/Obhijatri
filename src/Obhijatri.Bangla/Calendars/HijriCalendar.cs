using System.Globalization;

namespace Obhijatri.Bangla.Calendars;

/// <summary>A Hijri date. <see cref="Month"/> is 1 (Muharram) to 12 (Dhul Hijjah).</summary>
public readonly record struct HijriDate(int Year, int Month, int Day);

/// <summary>
/// Hijri dates from the Umm al-Qura calendar (the .NET calendar for Saudi Arabia, which needs no
/// network). Bangladesh follows its own moon sighting, which usually gives a day later than Saudi
/// Arabia, so the result can be shifted by a whole number of days.
/// </summary>
public static class HijriCalendar
{
    public const int MinAdjustment = -2;
    public const int MaxAdjustment = 2;

    private static readonly UmAlQuraCalendar Calendar = new();

    public static bool IsValidAdjustment(int days) => days is >= MinAdjustment and <= MaxAdjustment;

    public static HijriDate FromGregorian(DateOnly date, int adjustmentDays = 0)
    {
        var shifted = date.ToDateTime(TimeOnly.MinValue).AddDays(adjustmentDays);
        return new HijriDate(
            Calendar.GetYear(shifted),
            Calendar.GetMonth(shifted),
            Calendar.GetDayOfMonth(shifted));
    }
}
