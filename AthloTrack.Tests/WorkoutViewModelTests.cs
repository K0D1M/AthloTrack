using AthloTrack.Core.Models;
using AthloTrack.ViewModels;

namespace AthloTrack.Tests;

public sealed class WorkoutListItemTests
{
    private static WorkoutProgram Workout(DateTimeOffset? read = null, DateTimeOffset? completed = null) => new()
    {
        Id = Guid.NewGuid(),
        AthleteId = Guid.NewGuid(),
        Content = "5x5 squats",
        TargetDate = new DateOnly(2026, 10, 1),
        ReadAt = read,
        CompletedAt = completed,
    };

    [Fact]
    public void The_coach_sees_the_read_receipt_in_local_time()
    {
        var read = new DateTimeOffset(2026, 9, 30, 13, 26, 0, TimeSpan.Zero);

        var item = new WorkoutListItemViewModel(Workout(read), "Γιάννης", isCoach: true);

        Assert.True(item.ShowReadReceipt);
        Assert.Equal($"Διαβάστηκε από τον αθλητή στις {read.ToLocalTime():dd/MM/yyyy HH:mm}", item.ReadText);
    }

    [Fact]
    public void The_athlete_never_sees_the_read_receipt()
    {
        var item = new WorkoutListItemViewModel(Workout(DateTimeOffset.UtcNow), "Γιάννης", isCoach: false);

        Assert.False(item.ShowReadReceipt);
    }

    [Fact]
    public void An_unread_workout_has_no_receipt()
    {
        var item = new WorkoutListItemViewModel(Workout(), "Γιάννης", isCoach: true);

        Assert.False(item.ShowReadReceipt);
    }

    [Fact]
    public void Only_the_athlete_can_complete_and_only_open_workouts()
    {
        Assert.True(new WorkoutListItemViewModel(Workout(), "Γ", isCoach: false).CanComplete);
        Assert.False(new WorkoutListItemViewModel(Workout(), "Γ", isCoach: true).CanComplete);
        Assert.False(new WorkoutListItemViewModel(Workout(completed: DateTimeOffset.UtcNow), "Γ", isCoach: false).CanComplete);
    }
}

public sealed class WorkoutsViewModelTests
{
    [Fact]
    public async Task The_athlete_completes_an_open_workout()
    {
        var athleteId = Guid.NewGuid();
        var workouts = new FakeWorkouts();
        workouts.Items.Add(new WorkoutProgram { Id = Guid.NewGuid(), AthleteId = athleteId, Content = "run", TargetDate = new DateOnly(2026, 10, 1) });
        var vm = new WorkoutsViewModel(workouts, new FakeAthletes(), Sessions.Athlete(athleteId, Guid.NewGuid()));
        await vm.LoadAsync();

        await vm.CompleteWorkoutCommand.ExecuteAsync(vm.WorkoutPrograms[0]);
        Assert.True(vm.Confirm.IsOpen); // «Σίγουρα;» first (the default)
        Assert.Empty(workouts.Completed);
        await vm.Confirm.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal(new[] { workouts.Items[0].Id }, workouts.Completed);
    }

    [Fact]
    public async Task A_seen_workout_is_marked_read_once()
    {
        var athleteId = Guid.NewGuid();
        var workouts = new FakeWorkouts();
        workouts.Items.Add(new WorkoutProgram { Id = Guid.NewGuid(), AthleteId = athleteId, Content = "run", TargetDate = new DateOnly(2026, 10, 1) });
        var vm = new WorkoutsViewModel(workouts, new FakeAthletes(), Sessions.Athlete(athleteId, Guid.NewGuid()));
        await vm.LoadAsync();

        await vm.WorkoutSeenCommand.ExecuteAsync(vm.WorkoutPrograms[0]);
        await vm.WorkoutSeenCommand.ExecuteAsync(vm.WorkoutPrograms[0]);

        Assert.Single(workouts.Read);
    }

    [Fact]
    public async Task Workouts_seen_together_are_all_marked_read()
    {
        // Several workouts scroll into view at once; the first save is still running when the next arrives.
        var athleteId = Guid.NewGuid();
        var workouts = new FakeWorkouts { ReadGate = new TaskCompletionSource() };
        for (var i = 0; i < 3; i++)
            workouts.Items.Add(new WorkoutProgram { Id = Guid.NewGuid(), AthleteId = athleteId, Content = $"w{i}", TargetDate = new DateOnly(2026, 10, 1) });
        var vm = new WorkoutsViewModel(workouts, new FakeAthletes(), Sessions.Athlete(athleteId, Guid.NewGuid()));
        await vm.LoadAsync();

        foreach (var item in vm.WorkoutPrograms)
        {
            Assert.True(vm.WorkoutSeenCommand.CanExecute(item));
            vm.WorkoutSeenCommand.Execute(item);
        }
        workouts.ReadGate.SetResult();

        Assert.Equal(3, workouts.Read.Distinct().Count());
    }

    [Fact]
    public async Task The_coach_seeing_a_workout_does_not_mark_it_read()
    {
        var workouts = new FakeWorkouts();
        workouts.Items.Add(new WorkoutProgram { Id = Guid.NewGuid(), AthleteId = Guid.NewGuid(), Content = "run", TargetDate = new DateOnly(2026, 10, 1) });
        var vm = new WorkoutsViewModel(workouts, new FakeAthletes(), Sessions.Coach(Guid.NewGuid()));
        await vm.LoadAsync();

        await vm.WorkoutSeenCommand.ExecuteAsync(vm.WorkoutPrograms[0]);

        Assert.Empty(workouts.Read);
    }
}

public sealed class AthleteProfileViewModelTests
{
    private readonly Guid _coachId = Guid.NewGuid();
    private readonly Guid _athleteId = Guid.NewGuid();
    private readonly FakeWorkouts _workouts = new();
    private readonly FakeMeasurements _measurements = new();
    private readonly FakeAthletes _athletes = new();

    public AthleteProfileViewModelTests()
    {
        _athletes.Items.Add(new Athlete { Id = _athleteId, CoachId = _coachId, FullName = "Γιάννης Π." });
        _workouts.Items.Add(new WorkoutProgram { Id = Guid.NewGuid(), AthleteId = _athleteId, Content = "run", TargetDate = new DateOnly(2026, 10, 1) });
    }

    private async Task<AthleteProfileViewModel> Profile(Core.Auth.SessionState session)
    {
        var vm = new AthleteProfileViewModel(_athleteId, _athletes, _measurements, _workouts, session, new FakeAvatars());
        await vm.LoadAsync();
        return vm;
    }

    [Fact]
    public async Task No_chart_until_two_measurements()
    {
        _measurements.Items.Add(new Measurement { AthleteId = _athleteId, MeasuredAt = new DateOnly(2026, 9, 1), WeightKg = 80 });

        var vm = await Profile(Sessions.Coach(_coachId));

        Assert.Null(vm.Chart);
        Assert.False(vm.HasChartData);
    }

    [Fact]
    public async Task The_chart_has_a_line_per_recorded_metric_in_date_order()
    {
        _measurements.Items.Add(new Measurement { AthleteId = _athleteId, MeasuredAt = new DateOnly(2026, 9, 15), WeightKg = 79, FatMassWt = 20 });
        _measurements.Items.Add(new Measurement { AthleteId = _athleteId, MeasuredAt = new DateOnly(2026, 9, 1), WeightKg = 80 });

        var vm = await Profile(Sessions.Coach(_coachId));

        Assert.NotNull(vm.Chart);
        Assert.Equal(new[] { "01/09", "15/09" }, vm.Chart.Labels);
        Assert.Equal(new[] { "Βάρος (kg)", "Fat Mass/WT" }, vm.Chart.Lines.Select(l => l.Name)); // no fat/hgt recorded
        Assert.Equal(new double?[] { 80, 79 }, vm.Chart.Lines[0].Values);
        Assert.Equal(new double?[] { null, 20 }, vm.Chart.Lines[1].Values);
    }

    [Fact]
    public async Task Only_the_athlete_on_their_own_profile_marks_workouts_read()
    {
        var coachView = await Profile(Sessions.Coach(_coachId));
        await coachView.WorkoutSeenCommand.ExecuteAsync(coachView.Workouts[0]);
        Assert.Empty(_workouts.Read);

        var ownView = await Profile(Sessions.Athlete(_athleteId, _coachId));
        await ownView.WorkoutSeenCommand.ExecuteAsync(ownView.Workouts[0]);
        Assert.Single(_workouts.Read);
    }

    [Fact]
    public async Task The_coach_deletes_a_workout_after_confirming()
    {
        var vm = await Profile(Sessions.Coach(_coachId));
        var workout = vm.Workouts[0];

        vm.DeleteWorkoutCommand.Execute(workout);
        Assert.True(vm.IsConfirming);
        Assert.Empty(_workouts.Deleted);

        await vm.ConfirmCommand.ExecuteAsync(null);
        Assert.Equal(new[] { workout.Id }, _workouts.Deleted);
        Assert.Empty(vm.Workouts);
    }

    [Fact]
    public async Task The_athlete_cannot_edit_or_delete_workouts()
    {
        var vm = await Profile(Sessions.Athlete(_athleteId, _coachId));
        var editRequested = false;
        vm.EditWorkoutRequested += (_, _) => editRequested = true;

        vm.EditWorkoutCommand.Execute(vm.Workouts[0]);
        vm.DeleteWorkoutCommand.Execute(vm.Workouts[0]);

        Assert.False(editRequested);
        Assert.False(vm.IsConfirming);
    }
}

public sealed class AddWorkoutViewModelTests
{
    [Fact]
    public async Task Editing_updates_the_existing_workout()
    {
        var workouts = new FakeWorkouts();
        var existing = new WorkoutProgram { Id = Guid.NewGuid(), AthleteId = Guid.NewGuid(), Content = "old", TargetDate = new DateOnly(2026, 10, 1) };
        var vm = new AddWorkoutViewModel(existing.AthleteId, "Γιάννης", workouts, Sessions.Coach(Guid.NewGuid()));
        vm.BeginEdit(existing, "Γιάννης");
        var saved = false;
        vm.Saved += () => saved = true;

        vm.Content = "  new program  ";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(saved);
        Assert.Empty(workouts.Added);
        Assert.Equal((existing.Id, "new program", new DateOnly(2026, 10, 1), (bool?)null), Assert.Single(workouts.Updated));
        Assert.Equal("Επεξεργασία ασκησιολογίου", vm.PageTitle);
    }

    [Fact]
    public async Task A_new_workout_is_added_by_the_signed_in_coach()
    {
        var coachId = Guid.NewGuid();
        var workouts = new FakeWorkouts();
        var vm = new AddWorkoutViewModel(Guid.NewGuid(), "Γιάννης", workouts, Sessions.Coach(coachId)) { Content = "intervals" };

        await vm.SaveCommand.ExecuteAsync(null);

        var added = Assert.Single(workouts.Added);
        Assert.Equal(coachId, added.CreatedBy);
        Assert.Equal("Νέο ασκησιολόγιο", vm.PageTitle);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_new_workout_saves_the_coach_presence(bool present)
    {
        var workouts = new FakeWorkouts();
        var vm = new AddWorkoutViewModel(Guid.NewGuid(), "Γιάννης", workouts, Sessions.Coach(Guid.NewGuid())) { Content = "intervals" };

        if (present) vm.IsPresentChosen = true;
        else vm.IsAbsentChosen = true;
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(present, Assert.Single(workouts.Added).CoachPresent);
    }

    [Fact]
    public async Task Presence_is_optional_and_tapping_the_choice_again_clears_it()
    {
        var workouts = new FakeWorkouts();
        var vm = new AddWorkoutViewModel(Guid.NewGuid(), "Γιάννης", workouts, Sessions.Coach(Guid.NewGuid())) { Content = "intervals" };

        vm.IsPresentChosen = true;
        vm.IsAbsentChosen = true;   // switching
        Assert.False(vm.IsPresentChosen);
        Assert.False(vm.CoachPresent);

        vm.IsAbsentChosen = false;  // tapped again
        Assert.Null(vm.CoachPresent);

        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Null(Assert.Single(workouts.Added).CoachPresent);
    }

    [Fact]
    public async Task Editing_prefills_and_saves_a_changed_presence()
    {
        var workouts = new FakeWorkouts();
        var existing = new WorkoutProgram
        {
            Id = Guid.NewGuid(), AthleteId = Guid.NewGuid(), Content = "run", TargetDate = new DateOnly(2026, 10, 6), CoachPresent = true,
        };
        var vm = new AddWorkoutViewModel(existing.AthleteId, "Γιάννης", workouts, Sessions.Coach(Guid.NewGuid()));
        vm.BeginEdit(existing, "Γιάννης");
        Assert.True(vm.IsPresentChosen);

        vm.IsAbsentChosen = true;
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal((existing.Id, "run", new DateOnly(2026, 10, 6), (bool?)false), Assert.Single(workouts.Updated));
    }
}
