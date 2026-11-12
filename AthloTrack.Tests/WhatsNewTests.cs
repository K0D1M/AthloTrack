using System.Text;
using AthloTrack.Core.Auth;
using AthloTrack.Services;

namespace AthloTrack.Tests;

public sealed class WhatsNewTests
{
    private static readonly IReadOnlyList<Release> Log =
    [
        new("1.27-alpha", "2026-11-12", ["Ανανέωση", "Manrope"]),
        new("1.26-alpha", "2026-11-10", ["Έντυπα"]),
        new("1.25-alpha", "2026-11-05", []),
        new("1.3", "2026-10-04", ["Android"]),
    ];

    private static InMemoryAppPreferences Prefs(string? seen, bool usedBefore = true)
    {
        var p = new InMemoryAppPreferences();
        if (seen is not null) p.Set(WhatsNew.SeenKey, seen);
        if (usedBefore) p.Set(WhatsNew.UsedBeforeKey, "Coach");
        return p;
    }

    [Fact]
    public void After_an_update_it_lists_the_releases_since_the_last_one_seen_newest_first()
    {
        var shown = WhatsNew.TakePending(Prefs("1.24-alpha"), "1.27-alpha", Log);

        Assert.Equal(["1.27-alpha", "1.26-alpha"], shown.Select(r => r.Version)); // 1.25 has no changes
    }

    [Fact]
    public void It_shows_once_per_version()
    {
        var prefs = Prefs("1.26-alpha");

        Assert.Single(WhatsNew.TakePending(prefs, "1.27-alpha", Log));
        Assert.Empty(WhatsNew.TakePending(prefs, "1.27-alpha", Log));
    }

    [Fact]
    public void A_fresh_install_shows_nothing_but_remembers_the_version()
    {
        var prefs = Prefs(null, usedBefore: false);

        Assert.Empty(WhatsNew.TakePending(prefs, "1.27-alpha", Log));
        Assert.Equal("1.27-alpha", prefs.Get(WhatsNew.SeenKey));
    }

    [Fact]
    public void Updating_from_before_the_window_existed_shows_this_release()
    {
        var shown = WhatsNew.TakePending(Prefs(null), "1.27-alpha", Log);

        Assert.Equal(["1.27-alpha"], shown.Select(r => r.Version));
    }

    [Fact]
    public void A_version_without_changes_shows_no_window()
    {
        Assert.Empty(WhatsNew.TakePending(Prefs("1.24-alpha"), "1.25-alpha", Log));
        Assert.Empty(WhatsNew.TakePending(Prefs("1.27-alpha"), "1.28-alpha", Log)); // not in the changelog
    }

    [Theory]
    [InlineData("1.27-alpha", "1.3", 1)]
    [InlineData("1.3", "1.27-alpha", -1)]
    [InlineData("1.26-alpha", "1.26", 0)]
    [InlineData("2.0", "1.99", 1)]
    public void Versions_compare_by_their_numbers(string a, string b, int sign)
    {
        Assert.Equal(sign, Math.Sign(WhatsNew.Compare(a, b)));
    }

    [Fact]
    public void The_real_changelog_parses_and_has_this_version()
    {
        var path = Path.Combine(FindRepo(), "AthloTrack.Browser", "wwwroot", "version", "changelog.json");
        using var file = File.OpenRead(path);
        var releases = WhatsNew.Parse(file);

        Assert.Contains(releases, r => r.Version == AthloTrack.ViewModels.AppInfo.Version && r.Changes.Count > 0);
    }

    [Fact]
    public void The_window_text_has_a_heading_per_release_and_its_changes_as_a_list()
    {
        var text = WhatsNew.ToMarkup([new Release("1.27-alpha", "2026-11-12", ["Ανανέωση", "Manrope"])]);

        Assert.Equal("## Έκδοση 1.27-alpha · 12/11/2026\n- Ανανέωση\n- Manrope", text);
    }

    private static string FindRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AthloTrack.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("AthloTrack.slnx");
    }
}
