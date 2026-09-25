using Obhijatri.Core;
using Obhijatri.Core.Storage;

namespace Obhijatri.Tests;

public sealed class SessionAndSettingsTests
{
    [Fact]
    public void Session_RoundTrips_AndReplacesPrevious()
    {
        using var db = BrowserDatabase.OpenInMemory();
        var sessions = new SessionStore(db);

        sessions.Save([new SessionTab("https://old.example.com/", "old", true)]);
        sessions.Save(
        [
            new SessionTab("https://www.prothomalo.com/", "প্রথম আলো", false),
            new SessionTab(InternalPages.History, "ইতিহাস", true),
        ]);

        var loaded = sessions.Load();
        Assert.Equal(2, loaded.Count);
        Assert.Equal("প্রথম আলো", loaded[0].Title);
        Assert.True(loaded[1].IsActive);
    }

    [Fact]
    public void Settings_DefaultsAndOverwrites()
    {
        using var db = BrowserDatabase.OpenInMemory();
        var settings = new SettingsStore(db);

        Assert.True(settings.GetBool(SettingKeys.ShowBookmarkBar, true));
        settings.SetBool(SettingKeys.ShowBookmarkBar, false);
        settings.SetBool(SettingKeys.VerticalTabs, true);
        settings.SetBool(SettingKeys.VerticalTabs, false);

        Assert.False(settings.GetBool(SettingKeys.ShowBookmarkBar, true));
        Assert.False(settings.GetBool(SettingKeys.VerticalTabs, true));
    }

    [Fact]
    public void ClosedTabs_AreLastInFirstOut_AndCapped()
    {
        var stack = new ClosedTabStack(capacity: 3);
        for (var i = 0; i < 5; i++)
        {
            stack.Push(new ClosedTab($"https://{i}.example.com/", $"tab {i}", i));
        }

        Assert.Equal(3, stack.Count);
        Assert.True(stack.TryPop(out var top));
        Assert.Equal(4, top!.Index);
        stack.TryPop(out _);
        stack.TryPop(out var last);
        Assert.Equal(2, last!.Index);
        Assert.False(stack.TryPop(out _));
    }
}
