using Obhijatri.Bangla.Calendars;
using Obhijatri.Bangla.Prayer;

namespace Obhijatri.Tests;

public sealed class CalendarAndPrayerTests
{
    // ---- Bangla calendar ----

    [Theory]
    [InlineData(2026, 4, 14, 1433, 1, 1)]    // Pohela Boishakh
    [InlineData(2026, 4, 13, 1432, 12, 30)]  // last day of the year
    [InlineData(2025, 4, 14, 1432, 1, 1)]
    [InlineData(2026, 2, 14, 1432, 11, 1)]   // Pohela Falgun
    [InlineData(2026, 2, 21, 1432, 11, 8)]   // Language Movement Day: 8 Falgun
    [InlineData(2026, 3, 26, 1432, 12, 12)]  // Independence Day: 12 Choitro
    [InlineData(2026, 12, 16, 1433, 9, 1)]   // Victory Day: 1 Poush
    [InlineData(2025, 12, 16, 1432, 9, 1)]
    [InlineData(2026, 1, 1, 1432, 9, 17)]
    [InlineData(2026, 1, 15, 1432, 10, 1)]   // 1 Magh
    [InlineData(2026, 1, 14, 1432, 9, 30)]
    [InlineData(2026, 9, 15, 1433, 5, 31)]   // last day of Bhadro
    [InlineData(2026, 9, 16, 1433, 6, 1)]    // 1 Ashwin, which has 31 days
    [InlineData(2026, 10, 16, 1433, 6, 31)]
    [InlineData(2026, 10, 17, 1433, 7, 1)]   // 1 Kartik
    [InlineData(2026, 11, 16, 1433, 8, 1)]   // 1 Ogrohayon
    [InlineData(2026, 9, 29, 1433, 6, 14)]
    public void Bangla_KnownDates(int y, int m, int d, int by, int bm, int bd) =>
        Assert.Equal(new BanglaDate(by, bm, bd), BanglaCalendar.FromGregorian(new DateOnly(y, m, d)));

    [Theory]
    [InlineData(2027, 3, 14, 1433, 11, 29)]  // Falgun has 29 days in a normal year
    [InlineData(2027, 3, 15, 1433, 12, 1)]
    [InlineData(2028, 2, 29, 1434, 11, 16)]  // the leap day
    [InlineData(2028, 3, 14, 1434, 11, 30)]  // Falgun has 30 days in a leap year
    [InlineData(2028, 3, 15, 1434, 12, 1)]
    public void Bangla_LeapYearFalgun(int y, int m, int d, int by, int bm, int bd) =>
        Assert.Equal(new BanglaDate(by, bm, bd), BanglaCalendar.FromGregorian(new DateOnly(y, m, d)));

    [Fact]
    public void Bangla_EveryDayFollowsThePreviousOne()
    {
        var day = new DateOnly(2020, 1, 1);
        var previous = BanglaCalendar.FromGregorian(day);
        for (day = day.AddDays(1); day.Year <= 2035; day = day.AddDays(1))
        {
            var current = BanglaCalendar.FromGregorian(day);
            var sameMonth = current.Month == previous.Month && current.Year == previous.Year && current.Day == previous.Day + 1;
            var nextMonth = current.Day == 1 && previous.Day == BanglaCalendar.DaysInMonth(previous.Year, previous.Month)
                && (current.Month == previous.Month + 1 && current.Year == previous.Year
                    || previous.Month == 12 && current.Month == 1 && current.Year == previous.Year + 1);
            Assert.True(sameMonth || nextMonth, $"{day}: {previous} then {current}");
            previous = current;
        }
    }

    [Fact]
    public void Bangla_YearLengthsAre365Or366()
    {
        for (var year = 1427; year < 1440; year++)
        {
            var days = Enumerable.Range(1, 12).Sum(m => BanglaCalendar.DaysInMonth(year, m));
            var startsIn = year + 593;
            Assert.Equal(DateTime.IsLeapYear(startsIn + 1) ? 366 : 365, days);
        }
    }

    [Fact]
    public void Bangla_SeasonsAreTwoMonthsEach()
    {
        Assert.Equal(1, new BanglaDate(1433, 1, 1).Season);
        Assert.Equal(2, new BanglaDate(1433, 3, 1).Season);
        Assert.Equal(3, new BanglaDate(1433, 6, 1).Season); // Ashwin is in Shorot
        Assert.Equal(4, new BanglaDate(1433, 8, 1).Season);
        Assert.Equal(5, new BanglaDate(1433, 10, 1).Season);
        Assert.Equal(6, new BanglaDate(1433, 12, 1).Season);
    }

    // ---- Hijri ----

    [Fact]
    public void Hijri_RamadanAndEid2026_UmmAlQura()
    {
        Assert.Equal(new HijriDate(1447, 9, 1), HijriCalendar.FromGregorian(new DateOnly(2026, 2, 18)));
        Assert.Equal(new HijriDate(1447, 10, 1), HijriCalendar.FromGregorian(new DateOnly(2026, 3, 20)));
    }

    [Fact]
    public void Hijri_AdjustmentShiftsByWholeDays()
    {
        var eid = new DateOnly(2026, 3, 20);
        Assert.Equal(new HijriDate(1447, 9, 30), HijriCalendar.FromGregorian(eid, -1));
        Assert.Equal(new HijriDate(1447, 10, 2), HijriCalendar.FromGregorian(eid, 1));
    }

    [Theory]
    [InlineData(-2, true)]
    [InlineData(2, true)]
    [InlineData(-3, false)]
    [InlineData(3, false)]
    public void Hijri_AdjustmentRange(int days, bool valid) => Assert.Equal(valid, HijriCalendar.IsValidAdjustment(days));

    // ---- Districts ----

    [Fact]
    public void Districts_Are64_WithUniqueIdsAndPlausibleBangladeshCoordinates()
    {
        Assert.Equal(64, Districts.All.Count);
        Assert.Equal(64, Districts.All.Select(d => d.Id).Distinct().Count());
        Assert.All(Districts.All, d =>
        {
            Assert.InRange(d.Lat, 20.6, 26.7);
            Assert.InRange(d.Lon, 88.0, 92.7);
            Assert.False(string.IsNullOrWhiteSpace(d.Bn));
            Assert.False(string.IsNullOrWhiteSpace(d.En));
        });
    }

    [Fact]
    public void Districts_UnknownIdFallsBackToDhaka()
    {
        Assert.Equal("dhaka", Districts.Get("nowhere").Id);
        Assert.False(Districts.IsValidId("nowhere"));
        Assert.True(Districts.IsValidId("sylhet"));
    }

    // ---- Settings ----

    [Fact]
    public void Settings_NewTabDefaultsAndValidation()
    {
        using var db = Obhijatri.Core.Storage.BrowserDatabase.OpenInMemory();
        var store = new Obhijatri.Core.Storage.SettingsStore(db);
        var settings = new Obhijatri.Core.Settings.BrowserSettings(store);

        Assert.Equal("dhaka", settings.PrayerDistrict);
        Assert.Equal(0, settings.HijriAdjustment);

        settings.PrayerDistrict = "sylhet";
        settings.HijriAdjustment = -1;
        Assert.Equal("sylhet", settings.PrayerDistrict);
        Assert.Equal(-1, settings.HijriAdjustment);

        settings.PrayerDistrict = "atlantis";
        settings.HijriAdjustment = 9;
        Assert.Equal("dhaka", settings.PrayerDistrict);
        Assert.Equal(0, settings.HijriAdjustment);

        store.SetString(Obhijatri.Core.Settings.BrowserSettings.Keys.PrayerDistrict, "<script>");
        store.SetString(Obhijatri.Core.Settings.BrowserSettings.Keys.HijriAdjustment, "abc");
        Assert.Equal("dhaka", settings.PrayerDistrict);
        Assert.Equal(0, settings.HijriAdjustment);
    }

    // ---- Prayer times (reference: an independent implementation of the same method, Karachi angles and Hanafi Asr) ----

    private static void AssertClose(string expected, TimeOnly actual, int toleranceMinutes = 2)
    {
        var want = TimeOnly.ParseExact(expected, "HH:mm");
        var difference = Math.Abs((actual - want).TotalMinutes);
        Assert.True(difference <= toleranceMinutes, $"expected about {expected}, got {actual:HH:mm}");
    }

    [Theory]
    [InlineData("dhaka", 2026, 9, 29, "04:34", "05:49", "11:49", "16:08", "17:48", "19:03")]
    [InlineData("chattogram", 2026, 4, 14, "04:17", "05:34", "11:53", "16:23", "18:13", "19:29")]
    [InlineData("rangpur", 2026, 12, 21, "05:23", "06:45", "12:01", "15:41", "17:17", "18:39")]
    public void Prayer_MatchesReference(string district, int y, int m, int d, string fajr, string sunrise, string dhuhr, string asr, string maghrib, string isha)
    {
        var place = Districts.Get(district);
        var times = PrayerCalculator.Calculate(new DateOnly(y, m, d), place.Lat, place.Lon, Districts.UtcOffsetHours);
        AssertClose(fajr, times.Fajr);
        AssertClose(sunrise, times.Sunrise);
        AssertClose(dhuhr, times.Dhuhr);
        AssertClose(asr, times.Asr);
        AssertClose(maghrib, times.Maghrib);
        AssertClose(isha, times.Isha);
    }

    [Fact]
    public void Prayer_AlwaysInOrder_ForEveryDistrictAndDayOfTheYear()
    {
        foreach (var place in Districts.All)
        {
            for (var day = new DateOnly(2026, 1, 1); day.Year == 2026; day = day.AddDays(7))
            {
                var t = PrayerCalculator.Calculate(day, place.Lat, place.Lon, Districts.UtcOffsetHours);
                Assert.True(t.Fajr < t.Sunrise && t.Sunrise < t.Dhuhr && t.Dhuhr < t.Asr && t.Asr < t.Maghrib && t.Maghrib < t.Isha,
                    $"{place.Id} {day}: {t}");
            }
        }
    }

    [Fact]
    public void Prayer_EastIsEarlierThanWest_OnTheSameDay()
    {
        var day = new DateOnly(2026, 6, 1);
        var sylhet = Districts.Get("sylhet");
        var rajshahi = Districts.Get("rajshahi");
        var east = PrayerCalculator.Calculate(day, sylhet.Lat, sylhet.Lon, Districts.UtcOffsetHours);
        var west = PrayerCalculator.Calculate(day, rajshahi.Lat, rajshahi.Lon, Districts.UtcOffsetHours);
        Assert.True(east.Dhuhr < west.Dhuhr);
        Assert.True(east.Maghrib < west.Maghrib);
    }
}
