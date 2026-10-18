using AthloTrack.Core.Auth;
using AthloTrack.Services;

namespace AthloTrack.Tests;

/// <summary>«Νέα έκδοση»: version comparison, latest.json, the daily check.</summary>
public sealed class UpdateCheckerTests
{
    [Theory]
    [InlineData("1.20-alpha", "1.19-alpha", true)]
    [InlineData("1.19-alpha", "1.19-alpha", false)]
    [InlineData("1.9", "1.19-alpha", false)]
    [InlineData("1.100", "1.99", true)]
    [InlineData("2.0", "1.19-alpha", true)]
    [InlineData("1.19.1-alpha", "1.19-alpha", true)]
    [InlineData("1.18-alpha", "1.19-alpha", false)]
    public void Versions_compare_by_their_numbers(string candidate, string current, bool newer) =>
        Assert.Equal(newer, UpdateChecker.IsNewer(candidate, current));

    [Fact]
    public void A_newer_android_entry_with_a_link_is_an_update()
    {
        const string json = """{"android": {"version": "1.20-alpha", "url": "https://mega.nz/file/abc#key"}}""";

        var update = UpdateChecker.Parse(json, "android", "1.19-alpha");

        Assert.Equal(new UpdateInfo("1.20-alpha", "https://mega.nz/file/abc#key"), update);
    }

    [Theory]
    [InlineData("""{"android": {"version": "1.19-alpha", "url": "https://mega.nz/x"}}""")]   // same version
    [InlineData("""{"android": {"version": "1.20-alpha", "url": ""}}""")]                    // no link yet
    [InlineData("""{"android": {"version": "1.20-alpha", "url": "http://mega.nz/x"}}""")]    // not https
    [InlineData("""{"windows": {"version": "9.0", "url": "https://mega.nz/x"}}""")]          // other channel
    [InlineData("""{}""")]
    public void Anything_else_is_no_update(string json) =>
        Assert.Null(UpdateChecker.Parse(json, "android", "1.19-alpha"));

    [Fact]
    public async Task Without_a_channel_nothing_is_checked()
    {
        var before = UpdateChecker.Channel;
        UpdateChecker.Channel = null;
        try
        {
            Assert.False(UpdateChecker.IsSupported);
            Assert.Null(await UpdateChecker.CheckDailyAsync(new InMemoryAppPreferences(), DateTimeOffset.UtcNow));
        }
        finally
        {
            UpdateChecker.Channel = before;
        }
    }

    [Fact]
    public void Later_remembers_the_dismissed_version()
    {
        var prefs = new InMemoryAppPreferences();

        UpdateChecker.Dismiss(prefs, new UpdateInfo("1.20-alpha", "https://mega.nz/x"));

        Assert.Equal("1.20-alpha", prefs.Get("update.dismissed"));
    }
}
