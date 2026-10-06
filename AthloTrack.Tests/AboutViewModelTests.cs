using AthloTrack.ViewModels;

namespace AthloTrack.Tests;

public class AboutViewModelTests
{
    [Fact]
    public void Version_comes_from_the_project_without_the_commit_suffix()
    {
        var version = new AboutViewModel().Version;

        Assert.Matches(@"^\d+(\.\d+)+(-[0-9A-Za-z.]+)?$", version);
        Assert.NotEqual("1.0.0", version); // the SDK default, i.e. <Version> missing from AthloTrack.csproj
    }

    [Fact]
    public void Stage_is_the_part_after_the_dash()
    {
        var dash = AppInfo.Version.IndexOf('-');

        Assert.Equal(dash >= 0 ? AppInfo.Version[(dash + 1)..] : "", AppInfo.Stage);
        Assert.Equal(dash >= 0, AppInfo.IsPreRelease);
    }
}
