namespace Obhijatri.Bangla.Prayer;

/// <summary>The five daily prayers and sunrise, as clock times in the local zone of the place.</summary>
public sealed record PrayerTimes(TimeOnly Fajr, TimeOnly Sunrise, TimeOnly Dhuhr, TimeOnly Asr, TimeOnly Maghrib, TimeOnly Isha)
{
    /// <summary>Prayers in the order of the day, without sunrise.</summary>
    public IReadOnlyList<(PrayerKind Kind, TimeOnly Time)> Prayers =>
    [
        (PrayerKind.Fajr, Fajr),
        (PrayerKind.Dhuhr, Dhuhr),
        (PrayerKind.Asr, Asr),
        (PrayerKind.Maghrib, Maghrib),
        (PrayerKind.Isha, Isha),
    ];
}

public enum PrayerKind
{
    Fajr,
    Dhuhr,
    Asr,
    Maghrib,
    Isha,
}

/// <summary>
/// Prayer times computed on the device from the sun's position (no network). The method is the one
/// used in Bangladesh: Fajr and Isha at 18 degrees below the horizon (University of Islamic Sciences,
/// Karachi), and Asr when a shadow is twice the object's length (Hanafi). Sunrise and Maghrib are the
/// upper edge of the sun at the horizon, allowing for refraction. Times are rounded to the minute.
/// The formulas are the standard low-precision solar position ones (accurate to about a minute).
/// </summary>
public static class PrayerCalculator
{
    public const double FajrAngle = 18;
    public const double IshaAngle = 18;
    public const double SunriseSunsetAngle = 0.833;
    public const double AsrShadowFactor = 2;

    /// <param name="utcOffsetHours">The zone's offset from UTC (Bangladesh is 6).</param>
    public static PrayerTimes Calculate(DateOnly date, double latitude, double longitude, double utcOffsetHours)
    {
        var jd = JulianDate(date.Year, date.Month, date.Day) - longitude / (15 * 24);

        // Each time is found from the sun's position at about that time, so run twice.
        double fajr = 5, sunrise = 6, dhuhr = 12, asr = 13, maghrib = 18, isha = 18;
        for (var pass = 0; pass < 2; pass++)
        {
            dhuhr = MidDay(jd, dhuhr / 24);
            fajr = AngleTime(jd, latitude, dhuhr, fajr / 24, FajrAngle, before: true);
            sunrise = AngleTime(jd, latitude, dhuhr, sunrise / 24, SunriseSunsetAngle, before: true);
            asr = AsrTime(jd, latitude, dhuhr, asr / 24);
            maghrib = AngleTime(jd, latitude, dhuhr, maghrib / 24, SunriseSunsetAngle, before: false);
            isha = AngleTime(jd, latitude, dhuhr, isha / 24, IshaAngle, before: false);
        }

        var shift = utcOffsetHours - longitude / 15;
        return new PrayerTimes(
            ToTime(fajr + shift), ToTime(sunrise + shift), ToTime(dhuhr + shift),
            ToTime(asr + shift), ToTime(maghrib + shift), ToTime(isha + shift));
    }

    // ---- Solar position (fractions of a day for "t") ----

    private static (double Declination, double EquationOfTime) Sun(double jd)
    {
        var d = jd - 2451545.0;
        var g = Fix(357.529 + 0.98560028 * d, 360);
        var q = Fix(280.459 + 0.98564736 * d, 360);
        var l = Fix(q + 1.915 * Sin(g) + 0.020 * Sin(2 * g), 360);
        var e = 23.439 - 0.00000036 * d;
        var ra = Math.Atan2(Cos(e) * Sin(l), Cos(l)) * 180 / Math.PI / 15;
        var equation = q / 15 - Fix(ra, 24);
        var declination = Math.Asin(Sin(e) * Sin(l)) * 180 / Math.PI;
        return (declination, equation);
    }

    private static double MidDay(double jd, double t) => Fix(12 - Sun(jd + t).EquationOfTime, 24);

    /// <summary>Hours (solar) when the sun is <paramref name="angle"/> degrees below the horizon, before or after noon.</summary>
    private static double AngleTime(double jd, double latitude, double noon, double t, double angle, bool before)
    {
        var declination = Sun(jd + t).Declination;
        var ratio = (-Sin(angle) - Sin(declination) * Sin(latitude)) / (Cos(declination) * Cos(latitude));
        var hours = Math.Acos(Math.Clamp(ratio, -1, 1)) * 180 / Math.PI / 15;
        return noon + (before ? -hours : hours);
    }

    private static double AsrTime(double jd, double latitude, double noon, double t)
    {
        var declination = Sun(jd + t).Declination;
        var altitude = Math.Atan(1 / (AsrShadowFactor + Math.Tan(Math.Abs(latitude - declination) * Math.PI / 180))) * 180 / Math.PI;
        var ratio = (Sin(altitude) - Sin(declination) * Sin(latitude)) / (Cos(declination) * Cos(latitude));
        return noon + Math.Acos(Math.Clamp(ratio, -1, 1)) * 180 / Math.PI / 15;
    }

    // ---- Helpers ----

    private static double JulianDate(int year, int month, int day)
    {
        if (month <= 2)
        {
            year -= 1;
            month += 12;
        }
        var a = Math.Floor(year / 100.0);
        var b = 2 - a + Math.Floor(a / 4);
        return Math.Floor(365.25 * (year + 4716)) + Math.Floor(30.6001 * (month + 1)) + day + b - 1524.5;
    }

    private static TimeOnly ToTime(double hours)
    {
        var minutes = (int)Math.Round(Fix(hours, 24) * 60, MidpointRounding.AwayFromZero) % (24 * 60);
        return new TimeOnly(minutes / 60, minutes % 60);
    }

    private static double Fix(double value, double range)
    {
        var result = value - range * Math.Floor(value / range);
        return result < 0 ? result + range : result;
    }

    private static double Sin(double degrees) => Math.Sin(degrees * Math.PI / 180);

    private static double Cos(double degrees) => Math.Cos(degrees * Math.PI / 180);
}
