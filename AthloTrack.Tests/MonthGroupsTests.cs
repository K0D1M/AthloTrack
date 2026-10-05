using AthloTrack.Core.Models;
using AthloTrack.ViewModels;

namespace AthloTrack.Tests;

public class MonthGroupsTests
{
    private static WorkoutProgram W(int y, int m, int d) =>
        new() { Id = Guid.NewGuid(), Content = $"{d}/{m}/{y}", TargetDate = new DateOnly(y, m, d) };

    [Fact]
    public void Groups_are_months_newest_first_with_the_newest_open()
    {
        var groups = MonthGroups.Build(new[] { W(2026, 9, 3), W(2026, 10, 1), W(2025, 12, 20), W(2026, 10, 6), W(2025, 11, 2) });

        Assert.Equal(["Οκτώβριος 2026", "Σεπτέμβριος 2026", "Δεκέμβριος 2025", "Νοέμβριος 2025"], groups.Select(g => g.Title));
        Assert.Equal(["6/10/2026", "1/10/2026"], groups[0].Items.Select(w => w.Content));
        Assert.Equal("Οκτώβριος 2026 (2)", groups[0].Header);
        Assert.Equal([true, false, false, false], groups.Select(g => g.IsExpanded));
        // The year line appears where 2025 starts, not above the newest year.
        Assert.Equal([false, false, true, false], groups.Select(g => g.ShowYearSeparator));
        Assert.Equal("2025", groups[2].YearText);
    }

    [Fact]
    public void A_header_toggles_its_month()
    {
        var group = MonthGroups.Build(new[] { W(2026, 10, 1) })[0];
        Assert.True(group.IsExpanded);

        group.ToggleCommand.Execute(null);

        Assert.False(group.IsExpanded);
    }

    [Fact]
    public void A_reload_keeps_what_the_user_opened_and_closed()
    {
        var items = new[] { W(2026, 10, 1), W(2026, 9, 1), W(2026, 8, 1) };
        var before = MonthGroups.Build(items);
        before[0].IsExpanded = false; // closed October
        before[2].IsExpanded = true;  // opened August

        var after = MonthGroups.Build(items.Append(W(2026, 9, 15)), before);

        Assert.Equal([false, false, true], after.Select(g => g.IsExpanded));
        Assert.Equal(2, after[1].Count);
    }

    [Fact]
    public void Measurements_group_by_their_date()
    {
        var groups = MonthGroups.Build(new[]
        {
            new Measurement { MeasuredAt = new DateOnly(2026, 9, 4), WeightKg = 100 },
            new Measurement { MeasuredAt = new DateOnly(2026, 10, 4), WeightKg = 90 },
        });

        Assert.Equal(["Οκτώβριος 2026", "Σεπτέμβριος 2026"], groups.Select(g => g.Title));
    }

    [Fact]
    public async Task The_profile_shows_its_workouts_by_month()
    {
        var athleteId = Guid.NewGuid();
        var athletes = new FakeAthletes();
        athletes.Items.Add(new Athlete { Id = athleteId, FullName = "Γιάννης" });
        var workouts = new FakeWorkouts();
        workouts.Items.AddRange([W(2026, 10, 1), W(2026, 9, 1)]);
        foreach (var w in workouts.Items) w.AthleteId = athleteId;

        var vm = new AthleteProfileViewModel(athleteId, athletes, new FakeMeasurements(), workouts,
            Sessions.Coach(Guid.NewGuid()), new FakeAvatars());
        await vm.LoadAsync();

        Assert.Equal(["Οκτώβριος 2026", "Σεπτέμβριος 2026"], vm.WorkoutGroups.Select(g => g.Title));
        Assert.Empty(vm.MeasurementGroups);
    }
}

public class ThemeSettingTests
{
    private sealed class FakeTheme : IThemeService
    {
        public ThemeChoice Current { get; set; } = ThemeChoice.System;
        public List<ThemeChoice> Applied { get; } = new();
        public void Set(ThemeChoice choice) { Current = choice; Applied.Add(choice); }
    }

    [Fact]
    public void The_settings_page_shows_and_applies_the_theme()
    {
        var theme = new FakeTheme { Current = ThemeChoice.Dark };
        var vm = new SettingsViewModel(null!, new AthloTrack.Core.Auth.SessionState(), null!, null!, null!, theme);
        Assert.True(vm.IsDarkTheme);
        Assert.Empty(theme.Applied); // opening the page doesn't re-apply anything

        vm.IsLightTheme = true;
        Assert.Equal(ThemeChoice.Light, theme.Current);
        Assert.False(vm.IsDarkTheme);

        vm.IsLightTheme = false; // the radio button being unchecked doesn't change the choice
        Assert.Equal([ThemeChoice.Light], theme.Applied);
    }
}
