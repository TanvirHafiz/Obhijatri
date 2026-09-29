namespace Obhijatri.Bangla.Calendars;

/// <summary>A date in the Bangla calendar. <see cref="Month"/> is 1 (Boishakh) to 12 (Choitro).</summary>
public readonly record struct BanglaDate(int Year, int Month, int Day)
{
    /// <summary>The six seasons, 1 (Grishmo) to 6 (Boshonto), two months each.</summary>
    public int Season => (Month - 1) / 2 + 1;
}

/// <summary>
/// The revised Bangla calendar of Bangladesh (Bangla Academy, 1987, with the 2018 to 2019
/// changes): the year starts on 14 April; Boishakh to Ashwin have 31 days, Kartik to Magh 30,
/// Falgun 29 (30 in a leap year, that is when the Gregorian February that follows has 29 days)
/// and Choitro 30. The current rules are applied to every date, including years before the
/// revision, for which the official calendar used slightly different month lengths.
/// </summary>
public static class BanglaCalendar
{
    private const int YearOffset = 593;
    private const int NewYearMonth = 4;
    private const int NewYearDay = 14;
    private const int FalgunIndex = 10;

    private static readonly int[] MonthLengths = [31, 31, 31, 31, 31, 31, 30, 30, 30, 30, 29, 30];

    public static BanglaDate FromGregorian(DateOnly date)
    {
        var startYear = date >= new DateOnly(date.Year, NewYearMonth, NewYearDay) ? date.Year : date.Year - 1;
        var offset = date.DayNumber - new DateOnly(startYear, NewYearMonth, NewYearDay).DayNumber;

        for (var month = 0; month < 12; month++)
        {
            var length = MonthLength(startYear, month + 1);
            if (offset < length)
            {
                return new BanglaDate(startYear - YearOffset, month + 1, offset + 1);
            }
            offset -= length;
        }

        throw new InvalidOperationException("Day offset is past the end of the Bangla year.");
    }

    /// <summary>Days in <paramref name="month"/> of the Bangla year that starts in Gregorian <paramref name="startYear"/>.</summary>
    private static int MonthLength(int startYear, int month) =>
        month - 1 == FalgunIndex && DateTime.IsLeapYear(startYear + 1) ? 30 : MonthLengths[month - 1];

    /// <summary>Days in a month of a Bangla year (for example 1433).</summary>
    public static int DaysInMonth(int banglaYear, int month)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month));
        }
        return MonthLength(banglaYear + YearOffset, month);
    }
}
