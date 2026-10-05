using AthloTrack.ViewModels;

namespace AthloTrack.Tests;

public class AboutViewModelTests
{
    [Fact]
    public void Version_comes_from_the_project_without_the_commit_suffix()
    {
        var version = new AboutViewModel().Version;

        Assert.Matches(@"^\d+(\.\d+)+$", version);
        Assert.NotEqual("1.0.0", version); // the SDK default, i.e. <Version> missing from AthloTrack.csproj
    }
}
