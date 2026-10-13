using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;
using AthloTrack.Core.Models;
using AthloTrack.Services;
using AthloTrack.ViewModels;

namespace AthloTrack.Tests;

/// <summary>«Θα το κάνω / Δεν θα το κάνω» on a new workout, and the coach's «Ακύρωση» / «Επαναφορά».</summary>
public sealed class WorkoutChoiceTests
{
    private readonly Guid _coachId = Guid.NewGuid();
    private readonly Guid _athleteId = Guid.NewGuid();
    private readonly FakeWorkouts _workouts = new();
    private readonly FakeAthletes _athletes = new();

    public WorkoutChoiceTests()
    {
        _athletes.Items.Add(new Athlete { Id = _athleteId, CoachId = _coachId, FullName = "Γιάννης Π.", AuthUserId = Guid.NewGuid() });
    }

    private WorkoutProgram Add(Action<WorkoutProgram>? setup = null)
    {
        var w = new WorkoutProgram { Id = Guid.NewGuid(), AthleteId = _athleteId, Content = "run", TargetDate = new DateOnly(2026, 10, 14) };
        setup?.Invoke(w);
        _workouts.Items.Add(w);
        return w;
    }

    private async Task<AthleteProfileViewModel> Profile(Core.Auth.SessionState session, bool confirm = true)
    {
        var settings = new WorkoutAnswerSettings(new InMemoryAppPreferences()) { ConfirmAnswers = confirm };
        var vm = new AthleteProfileViewModel(_athleteId, _athletes, new FakeMeasurements(), _workouts, session,
            new FakeAvatars(), answerSettings: settings);
        await vm.LoadAsync();
        return vm;
    }

    private async Task<WorkoutsViewModel> List(Core.Auth.SessionState session, bool confirm = true)
    {
        var settings = new WorkoutAnswerSettings(new InMemoryAppPreferences()) { ConfirmAnswers = confirm };
        var vm = new WorkoutsViewModel(_workouts, _athletes, session, settings);
        await vm.LoadAsync();
        return vm;
    }

    // ---- the state ----

    [Fact]
    public void A_new_workout_waits_for_the_choice_and_takes_no_final_answer_yet()
    {
        var w = new WorkoutProgram();

        Assert.True(w.IsPending);
        Assert.False(w.CanAnswerOutcome);
        Assert.True(w.CanBeCancelled);
        Assert.True(w.CanBeReminded);
        Assert.False(w.IsClosed);
    }

    [Fact]
    public void Accepted_it_takes_the_final_answer()
    {
        var w = new WorkoutProgram { AcceptedAt = DateTimeOffset.UtcNow };

        Assert.False(w.IsPending);
        Assert.True(w.CanAnswerOutcome);
        Assert.True(w.ShowAccepted);
        Assert.True(w.CanBeCancelled);

        w.CompletedAt = DateTimeOffset.UtcNow;
        Assert.False(w.CanAnswerOutcome);
        Assert.False(w.ShowAccepted);
        Assert.False(w.CanBeCancelled);
        Assert.True(w.IsClosed);
    }

    [Fact]
    public void Declined_is_closed_no_answer_no_reminder_no_cancel()
    {
        var w = new WorkoutProgram { DeclinedAt = DateTimeOffset.UtcNow, DeclineReason = "  τραυματισμός " };

        Assert.False(w.IsPending);
        Assert.False(w.CanAnswerOutcome);
        Assert.False(w.CanBeReminded);
        Assert.False(w.CanBeCancelled);
        Assert.True(w.HasDeclineReason);
        Assert.Equal("✗ Δεν θα το κάνει: τραυματισμός", WorkoutListItemViewModel.DeclineLine(w, isCoach: true));
        Assert.Equal("✗ Δεν θα το κάνεις: τραυματισμός", WorkoutListItemViewModel.DeclineLine(w, isCoach: false));
    }

    [Fact]
    public void Cancelled_takes_nothing_until_restored()
    {
        var pending = new WorkoutProgram { CancelledAt = DateTimeOffset.UtcNow };
        var accepted = new WorkoutProgram { CancelledAt = DateTimeOffset.UtcNow, AcceptedAt = DateTimeOffset.UtcNow };

        foreach (var w in new[] { pending, accepted })
        {
            Assert.False(w.IsPending);
            Assert.False(w.CanAnswerOutcome);
            Assert.False(w.CanBeReminded);
            Assert.False(w.CanBeCancelled);
            Assert.False(w.ShowAccepted);
        }

        accepted.CancelledAt = null;
        Assert.True(accepted.CanAnswerOutcome);
    }

    [Fact]
    public void The_list_item_shows_each_side_its_buttons()
    {
        var pending = new WorkoutProgram { TargetDate = new DateOnly(2026, 10, 14) };
        var athlete = new WorkoutListItemViewModel(pending, "Γ", isCoach: false);
        var coach = new WorkoutListItemViewModel(pending, "Γ", isCoach: true, athleteHasLogin: true);

        Assert.True(athlete.CanAccept);
        Assert.False(athlete.CanComplete);
        Assert.False(athlete.CanCancel);
        Assert.False(coach.CanAccept);
        Assert.True(coach.CanCancel);
        Assert.True(coach.CanRemind);
        Assert.True(coach.ShowPending);
        Assert.False(athlete.ShowPending);

        var cancelled = new WorkoutListItemViewModel(new WorkoutProgram { CancelledAt = DateTimeOffset.UtcNow }, "Γ",
            isCoach: true, athleteHasLogin: true);
        Assert.True(cancelled.CanRestore);
        Assert.False(cancelled.CanCancel);
        Assert.False(cancelled.CanRemind);
        Assert.Equal(0.55, cancelled.CardOpacity);
    }

    // ---- the athlete ----

    [Fact]
    public async Task Accept_asks_first_then_sends()
    {
        var w = Add();
        var vm = await Profile(Sessions.Athlete(_athleteId, _coachId));

        await vm.AcceptWorkoutCommand.ExecuteAsync(w);
        Assert.True(vm.IsConfirming);
        Assert.Equal("Θα το κάνω", vm.ConfirmButtonText);
        Assert.False(vm.ConfirmWantsReason);
        Assert.Empty(_workouts.Accepted);

        await vm.ConfirmCommand.ExecuteAsync(null);
        Assert.Equal(new[] { w.Id }, _workouts.Accepted);
    }

    [Fact]
    public async Task With_the_setting_off_accept_goes_straight_away()
    {
        var w = Add();
        var vm = await Profile(Sessions.Athlete(_athleteId, _coachId), confirm: false);

        await vm.AcceptWorkoutCommand.ExecuteAsync(w);

        Assert.False(vm.IsConfirming);
        Assert.Equal(new[] { w.Id }, _workouts.Accepted);
    }

    [Fact]
    public async Task Decline_always_asks_and_sends_the_trimmed_reason()
    {
        var w = Add();
        var vm = await Profile(Sessions.Athlete(_athleteId, _coachId), confirm: false);

        vm.DeclineWorkoutCommand.Execute(w);
        Assert.True(vm.IsConfirming);
        Assert.True(vm.ConfirmWantsReason);
        vm.ConfirmReason = "  τραυματισμός  ";
        await vm.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal(new[] { (w.Id, (string?)"τραυματισμός") }, _workouts.Declined);
    }

    [Fact]
    public async Task Decline_without_a_reason_sends_null()
    {
        var w = Add();
        var vm = await List(Sessions.Athlete(_athleteId, _coachId));

        vm.DeclineWorkoutCommand.Execute(vm.WorkoutPrograms[0]);
        Assert.True(vm.Confirm.IsOpen);
        Assert.True(vm.Confirm.WantsReason);
        vm.Confirm.Reason = "   ";
        await vm.Confirm.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal(new[] { (w.Id, (string?)null) }, _workouts.Declined);
        Assert.False(vm.Confirm.WantsReason); // the next dialog starts clean
    }

    [Fact]
    public async Task A_pending_workout_takes_no_final_answer()
    {
        var w = Add();
        var vm = await Profile(Sessions.Athlete(_athleteId, _coachId), confirm: false);

        await vm.CompleteWorkoutCommand.ExecuteAsync(w);
        await vm.NotCompleteWorkoutCommand.ExecuteAsync(w);

        Assert.Empty(_workouts.Completed);
        Assert.Empty(_workouts.NotCompleted);
    }

    [Fact]
    public async Task The_coach_cannot_choose_for_the_athlete()
    {
        var w = Add();
        var vm = await Profile(Sessions.Coach(_coachId), confirm: false);

        await vm.AcceptWorkoutCommand.ExecuteAsync(w);
        vm.DeclineWorkoutCommand.Execute(w);

        Assert.Empty(_workouts.Accepted);
        Assert.False(vm.IsConfirming);
    }

    // ---- the coach ----

    [Fact]
    public async Task Cancel_asks_first_and_restore_does_not()
    {
        var w = Add();
        var vm = await Profile(Sessions.Coach(_coachId));

        vm.CancelWorkoutCommand.Execute(w);
        Assert.True(vm.IsConfirming);
        Assert.Equal("Ακύρωση του ασκησιολογίου της 14/10; Ο αθλητής θα ειδοποιηθεί.", vm.ConfirmMessage);
        Assert.True(vm.ConfirmIsDanger);
        await vm.ConfirmCommand.ExecuteAsync(null);
        Assert.Equal(new[] { w.Id }, _workouts.Cancelled);

        await vm.RestoreWorkoutCommand.ExecuteAsync(vm.Workouts[0]);
        Assert.Equal(new[] { w.Id }, _workouts.Restored);
        Assert.False(vm.Workouts[0].IsCancelled);
    }

    [Fact]
    public async Task The_athlete_cannot_cancel()
    {
        var w = Add();
        var vm = await Profile(Sessions.Athlete(_athleteId, _coachId));

        vm.CancelWorkoutCommand.Execute(w);

        Assert.False(vm.IsConfirming);
    }

    [Fact]
    public async Task A_refused_cancel_says_why()
    {
        Add();
        _workouts.CancelRefusal = CancelRefusal.AlreadyAnswered;
        var vm = await List(Sessions.Coach(_coachId));

        vm.CancelWorkoutCommand.Execute(vm.WorkoutPrograms[0]);
        await vm.Confirm.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal("Ο αθλητής έχει ήδη απαντήσει γι' αυτό το ασκησιολόγιο.", vm.ErrorMessage);
        Assert.Empty(_workouts.Cancelled);
    }

    [Theory]
    [InlineData("workout_accepted")]
    [InlineData("workout_declined")]
    [InlineData("workout_cancelled")]
    [InlineData("workout_restored")]
    public void The_new_notifications_open_the_workouts(string type)
    {
        NotificationNavigation.TakePending();
        NotificationNavigation.RequestFor(type, Guid.NewGuid().ToString());

        Assert.Equal(NotificationNavigation.WorkoutsSection, NotificationNavigation.TakePending()?.Section);
    }
}
