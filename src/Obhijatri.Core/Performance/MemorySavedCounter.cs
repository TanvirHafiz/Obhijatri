using System.Globalization;
using Obhijatri.Core.Storage;

namespace Obhijatri.Core.Performance;

/// <summary>
/// "Memory saved today": the sum of what sleeping tabs gave back. Stored as the day and the byte
/// count, so the figure starts again at zero on a new (local) day and survives a restart.
/// </summary>
public sealed class MemorySavedCounter
{
    public const string DayKey = "stats.memorySavedDay";
    public const string BytesKey = "stats.memorySavedBytes";

    private readonly SettingsStore _store;
    private readonly TimeProvider _time;

    public MemorySavedCounter(SettingsStore store, TimeProvider? time = null)
    {
        _store = store;
        _time = time ?? TimeProvider.System;
    }

    public long TodayBytes =>
        _store.GetString(DayKey) == Today()
        && long.TryParse(_store.GetString(BytesKey), NumberStyles.None, CultureInfo.InvariantCulture, out var bytes)
            ? bytes
            : 0;

    /// <summary>Adds to today's total. Zero or negative amounts (memory grew) are ignored.</summary>
    public void Add(long bytes)
    {
        if (bytes <= 0)
        {
            return;
        }

        var total = TodayBytes + bytes;
        _store.SetString(DayKey, Today());
        _store.SetString(BytesKey, total.ToString(CultureInfo.InvariantCulture));
    }

    private string Today() => _time.GetLocalNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
