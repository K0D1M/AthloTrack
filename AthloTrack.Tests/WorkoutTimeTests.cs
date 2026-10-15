using AthloTrack.Core.Auth;
using AthloTrack.Core.Models;
using AthloTrack.ViewModels;

namespace AthloTrack.Tests;

/// <summary>The optional start time of a workout: form, draft, and how it's shown.</summary>
public sealed class WorkoutTimeTests
{
    private readonly Guid _athleteId = Guid.NewGuid();

    private AddWorkoutViewModel Create(IAppPreferences prefs, FakeWorkouts workouts) =>
        new(_athleteId, "Γιάννης", workouts, Sessions.Coach(Guid.NewGuid()), prefs, new FakeTemplates(), new FakeAthletes());

    [Fact]
    public void The_time_follows_the_date_only_when_set()
    {
        var w = new WorkoutProgram { TargetDate = new DateOnly(2026, 10, 21) };
        Assert.Equal("21/10/2026", w.WhenText);

        w.StartTime = new TimeOnly(18, 30);
        Assert.Equal("21/10/2026 · 18:30", w.WhenText);
        Assert.Equal("Ημ. στόχος: 21/10/2026 · 18:30", new WorkoutListItemViewModel(w, "Γ").TargetDateText);
        Assert.EndsWith("21/10/2026 · 18:30",
            new CalendarEntryViewModel(w.TargetDate, Guid.Empty, "Γ", null, "run", w.StartTime).DateText);
    }

    [Fact]
    public async Task A_new_workout_saves_its_time()
    {
        var workouts = new FakeWorkouts();
        var vm = Create(new InMemoryAppPreferences(), workouts);
        vm.Content = "3x10 καθίσματα";
        vm.StartTime = new TimeSpan(7, 15, 0);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new TimeOnly(7, 15), Assert.Single(workouts.Added).StartTime);
    }

    [Fact]
    public async Task An_edit_keeps_or_clears_the_time()
    {
        var workouts = new FakeWorkouts();
        var existing = new WorkoutProgram
        {
            Id = Guid.NewGuid(), AthleteId = _athleteId, Content = "run", TargetDate = new DateOnly(2026, 10, 6),
            StartTime = new TimeOnly(18, 30),
        };
        var vm = Create(new InMemoryAppPreferences(), workouts);
        vm.BeginEdit(existing, "Γιάννης");
        Assert.Equal(new TimeSpan(18, 30, 0), vm.StartTime);
        Assert.True(vm.HasStartTime);

        await vm.SaveCommand.ExecuteAsync(null);
        vm.ClearStartTimeCommand.Execute(null);
        Assert.False(vm.HasStartTime);
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new TimeOnly?[] { new TimeOnly(18, 30), null }, workouts.UpdatedTimes);
    }

    [Fact]
    public void A_time_change_alone_is_kept_as_a_draft()
    {
        var prefs = new InMemoryAppPreferences();
        var first = Create(prefs, new FakeWorkouts());
        first.StartTime = new TimeSpan(6, 0, 0);

        var reopened = Create(prefs, new FakeWorkouts());

        Assert.True(reopened.IsDraftRestored);
        Assert.Equal(new TimeSpan(6, 0, 0), reopened.StartTime);
    }

    [Fact]
    public void A_draft_from_before_the_time_still_comes_back()
    {
        var prefs = new InMemoryAppPreferences();
        prefs.Set($"workout.draft.new.{_athleteId}", string.Join('\u001F', "1", "", "2026-10-21", "1", "παλιό πρόχειρο"));

        var vm = Create(prefs, new FakeWorkouts());

        Assert.True(vm.IsDraftRestored);
        Assert.Equal("παλιό πρόχειρο", vm.Content);
        Assert.True(vm.CoachPresent);
        Assert.Null(vm.StartTime);
    }
}
