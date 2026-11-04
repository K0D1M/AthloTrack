using AthloTrack.Core.Models;
using AthloTrack.ViewModels;

namespace AthloTrack.Tests;

/// <summary>Ημερολόγιο: a green ✓ or red ✕ on the athlete once the workout is answered.</summary>
public sealed class CalendarAnswerTests
{
    [Fact]
    public async Task Each_entry_carries_the_athletes_answer()
    {
        var athlete = new Athlete { Id = Guid.NewGuid(), FullName = "Μαρία" };
        var athletes = new FakeAthletes();
        athletes.Items.Add(athlete);
        var workouts = new FakeWorkouts();
        var day = DateOnly.FromDateTime(DateTime.Today);
        WorkoutProgram Add(string content, Action<WorkoutProgram>? set = null)
        {
            var w = new WorkoutProgram { Id = Guid.NewGuid(), AthleteId = athlete.Id, Content = content, TargetDate = day };
            set?.Invoke(w);
            workouts.Items.Add(w);
            return w;
        }
        Add("έγινε", w => { w.AcceptedAt = DateTimeOffset.UtcNow; w.CompletedAt = DateTimeOffset.UtcNow; });
        Add("δεν έγινε", w => { w.AcceptedAt = DateTimeOffset.UtcNow; w.NotCompletedAt = DateTimeOffset.UtcNow; });
        Add("ανοιχτό");

        var vm = new CalendarViewModel(workouts, athletes, new FakeAvatars());
        await vm.LoadAsync();
        var byContent = vm.Entries.ToDictionary(e => e.Content);

        Assert.True(byContent["έγινε"].IsDone);
        Assert.Equal("Ολοκληρώθηκε", byContent["έγινε"].StatusText);
        Assert.True(byContent["δεν έγινε"].IsNotDone);
        Assert.Equal("Δεν ολοκληρώθηκε", byContent["δεν έγινε"].StatusText);
        Assert.False(byContent["ανοιχτό"].IsAnswered); // no mark until the athlete answers

        // Tapping an entry opens the athlete's profile at that very workout.
        (Guid Athlete, Guid? Workout)? opened = null;
        vm.OpenWorkoutRequested += (a, w) => opened = (a, w);
        vm.OpenEntryCommand.Execute(byContent["δεν έγινε"]);
        Assert.Equal((athlete.Id, workouts.Items[1].Id), opened);
    }
}
