using AthloTrack.Core.Auth;
using AthloTrack.Core.Models;
using AthloTrack.ViewModels;

namespace AthloTrack.Tests;

/// <summary>«Παλιό ασκησιολόγιο»: workouts from before the app, saved already answered.</summary>
public sealed class PastWorkoutTests
{
    private readonly Guid _athleteId = Guid.NewGuid();
    private readonly FakeWorkouts _workouts = new();

    private AddWorkoutViewModel Create() =>
        new(_athleteId, "Γιάννης", _workouts, Sessions.Coach(Guid.NewGuid()),
            new InMemoryAppPreferences(), new FakeTemplates(), new FakeAthletes());

    private static DateTimeOffset Day(int offset) =>
        new(DateOnly.FromDateTime(DateTime.Now).AddDays(offset).ToDateTime(TimeOnly.MinValue));

    [Fact]
    public void Only_a_new_workout_before_today_is_past()
    {
        var vm = Create();
        vm.TargetDate = Day(0);
        Assert.False(vm.IsPast);
        vm.TargetDate = Day(-1);
        Assert.True(vm.IsPast);
        Assert.True(vm.PastCompleted); // «Ολοκληρώθηκε» by default

        var edit = Create();
        edit.BeginEdit(new WorkoutProgram { Id = Guid.NewGuid(), AthleteId = _athleteId, Content = "run", TargetDate = DateOnly.FromDateTime(DateTime.Now).AddDays(-30) }, "Γιάννης");
        Assert.False(edit.IsPast); // editing never turns a workout into an archived one
    }

    [Fact]
    public async Task A_past_date_is_saved_answered_without_the_normal_insert()
    {
        var vm = Create();
        var saved = false;
        vm.Saved += () => saved = true;
        vm.TargetDate = Day(-200);
        vm.Content = "Καθίσματα 5×5";
        vm.PastNotCompleted = true;
        vm.StartTime = new TimeSpan(18, 0, 0);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(saved);
        Assert.Empty(_workouts.Added);
        var past = Assert.Single(_workouts.AddedPast);
        Assert.Equal(_athleteId, past.Athlete);
        Assert.Equal(DateOnly.FromDateTime(Day(-200).DateTime), past.Date);
        Assert.False(past.Completed);
        Assert.Equal(new TimeOnly(18, 0), past.Time);
    }

    [Fact]
    public async Task Today_or_later_is_a_normal_workout()
    {
        var vm = Create();
        vm.TargetDate = Day(0);
        vm.Content = "Τρέξιμο 40΄";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Single(_workouts.Added);
        Assert.Empty(_workouts.AddedPast);
    }

    [Fact]
    public async Task Save_and_new_keeps_the_form_for_the_next_day()
    {
        var vm = Create();
        var closed = false;
        vm.Saved += () => closed = true;
        vm.TargetDate = Day(-10);
        vm.Content = "Πρώτο";

        await vm.SaveAndNewCommand.ExecuteAsync(null);

        Assert.False(closed);
        Assert.Single(_workouts.AddedPast);
        Assert.Equal(string.Empty, vm.Content);
        Assert.Equal(Day(-9).Date, vm.TargetDate.Date);
        Assert.Contains("Αποθηκεύτηκε", vm.InfoMessage);
    }

    [Fact]
    public void An_archived_workout_shows_its_tag_and_no_coach_prompts()
    {
        var w = new WorkoutProgram
        {
            TargetDate = new DateOnly(2025, 3, 12), FromArchive = true,
            AcceptedAt = DateTimeOffset.UtcNow, ReadAt = DateTimeOffset.UtcNow, CompletedAt = DateTimeOffset.UtcNow,
        };

        var item = new WorkoutListItemViewModel(w, "Γ", isCoach: true, athleteHasLogin: true);

        Assert.True(item.FromArchive);
        Assert.False(item.ShowReadReceipt);
        Assert.False(item.CanRemind);
        Assert.False(item.CanCancel);
        Assert.False(w.ShowRead);
    }
}
