namespace Obhijatri.Core.Performance;

/// <summary>When a background tab may be put to sleep (its page suspended to free memory).</summary>
public static class TabSleepPolicy
{
    /// <summary>Minutes of inactivity before a tab sleeps. 0 turns sleeping off.</summary>
    public static readonly IReadOnlyList<int> AllowedMinutes = [0, 5, 10, 15, 30, 60];

    public const int DefaultMinutes = 10;

    public static bool IsValidMinutes(int minutes) => AllowedMinutes.Contains(minutes);

    /// <summary>
    /// A tab sleeps only when sleeping is on, it is not the one being viewed, nothing is playing
    /// or loading, it is not pinned, and it has been idle long enough.
    /// </summary>
    public static bool ShouldSleep(
        int sleepAfterMinutes,
        TimeSpan idleFor,
        bool isActive,
        bool isPinned,
        bool isPlayingAudio,
        bool isLoading,
        bool isSleeping) =>
        sleepAfterMinutes > 0
        && !isActive
        && !isPinned
        && !isPlayingAudio
        && !isLoading
        && !isSleeping
        && idleFor >= TimeSpan.FromMinutes(sleepAfterMinutes);
}
