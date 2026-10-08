using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;
using AthloTrack.Core.Models;
using AthloTrack.Services;
using AthloTrack.ViewModels;

namespace AthloTrack.Tests;

/// <summary>Weeks, the two final answers, the optional confirmation, reminders, times and links.</summary>
public sealed class WorkoutAnswerTests
{
    private readonly Guid _coachId = Guid.NewGuid();
    private readonly Guid _athleteId = Guid.NewGuid();
    private readonly FakeWorkouts _workouts = new();
    private readonly FakeAthletes _athletes = new();

    public WorkoutAnswerTests()
    {
        _athletes.Items.Add(new Athlete { Id = _athleteId, CoachId = _coachId, FullName = "Γιάννης Π.", AuthUserId = Guid.NewGuid() });
    }

    private WorkoutProgram Add(int day, int month = 10, Action<WorkoutProgram>? setup = null)
    {
        var w = new WorkoutProgram { Id = Guid.NewGuid(), AthleteId = _athleteId, Content = $"w{day}", TargetDate = new DateOnly(2026, month, day) };
        setup?.Invoke(w);
        _workouts.Items.Add(w);
        return w;
    }

    private async Task<AthleteProfileViewModel> Profile(Core.Auth.SessionState session, bool confirm = true, Guid? focus = null)
    {
        var settings = new WorkoutAnswerSettings(new InMemoryAppPreferences()) { ConfirmAnswers = confirm };
        var vm = new AthleteProfileViewModel(_athleteId, _athletes, new FakeMeasurements(), _workouts, session,
            new FakeAvatars(), answerSettings: settings, focusWorkoutId: focus);
        await vm.LoadAsync();
        return vm;
    }

    // ---- weeks ----

    [Fact]
    public void A_month_is_split_in_7_day_blocks_newest_first()
    {
        foreach (var day in new[] { 1, 7, 8, 14, 22, 29, 31 }) Add(day);

        var month = Assert.Single(MonthGroups.Build(_workouts.Items));

        Assert.Equal(new[] { 5, 4, 2, 1 }, month.Weeks.Select(w => w.Number));
        Assert.Equal("Εβδομάδα 5 · 29–31/10", month.Weeks[0].Title);
        Assert.Equal("Εβδομάδα 5 · 29–31/10 (2)", month.Weeks[0].Header);
        Assert.Equal("Εβδομάδα 1 · 1–7/10", month.Weeks[^1].Title);
        Assert.Equal(2, month.Weeks[0].Items.Count);   // 29, 31
        Assert.Equal(2, month.Weeks[^1].Items.Count);  // 1, 7
    }

    [Fact]
    public void Only_the_newest_week_of_the_newest_month_starts_open_and_choices_survive_a_reload()
    {
        Add(3); Add(20);              // October: weeks 1 and 3
        Add(10, month: 9);            // September: week 2

        var months = MonthGroups.Build(_workouts.Items);
        Assert.Equal(new[] { true, false }, months[0].Weeks.Select(w => w.IsExpanded));
        Assert.False(months[1].Weeks[0].IsExpanded);

        months[0].Weeks[0].ToggleCommand.Execute(null);   // close week 3
        months[0].Weeks[1].ToggleCommand.Execute(null);   // open week 1
        var rebuilt = MonthGroups.Build(_workouts.Items, months);

        Assert.Equal(new[] { false, true }, rebuilt[0].Weeks.Select(w => w.IsExpanded));
    }

    [Fact]
    public void The_last_week_of_February_is_cut_at_the_month_end()
    {
        Add(28, month: 2);

        var week = Assert.Single(Assert.Single(MonthGroups.Build(_workouts.Items)).Weeks);

        Assert.Equal("Εβδομάδα 4 · 22–28/02", week.Title);
    }

    // ---- answers ----

    [Fact]
    public async Task Not_completed_asks_first_then_answers()
    {
        var w = Add(5);
        var vm = await Profile(Sessions.Athlete(_athleteId, _coachId));

        await vm.NotCompleteWorkoutCommand.ExecuteAsync(vm.Workouts[0]);
        Assert.True(vm.IsConfirming);
        Assert.Equal("Σίγουρα; Αυτό δεν μπορεί να αναιρεθεί.", vm.ConfirmMessage);
        Assert.Equal("Δεν ολοκληρώθηκε", vm.ConfirmButtonText);
        Assert.False(vm.ConfirmIsDanger);
        Assert.Empty(_workouts.NotCompleted);

        await vm.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal(new[] { w.Id }, _workouts.NotCompleted);
        Assert.True(vm.Workouts[0].IsAnswered);
    }

    [Fact]
    public async Task With_the_setting_off_the_answer_goes_straight_away()
    {
        var w = Add(5);
        var vm = await Profile(Sessions.Athlete(_athleteId, _coachId), confirm: false);

        await vm.CompleteWorkoutCommand.ExecuteAsync(vm.Workouts[0]);

        Assert.False(vm.IsConfirming);
        Assert.Equal(new[] { w.Id }, _workouts.Completed);
    }

    [Fact]
    public async Task An_answered_workout_takes_no_second_answer()
    {
        Add(5, setup: w => w.NotCompletedAt = DateTimeOffset.UtcNow);
        var vm = await Profile(Sessions.Athlete(_athleteId, _coachId), confirm: false);

        await vm.CompleteWorkoutCommand.ExecuteAsync(vm.Workouts[0]);
        await vm.NotCompleteWorkoutCommand.ExecuteAsync(vm.Workouts[0]);

        Assert.Empty(_workouts.Completed);
        Assert.Empty(_workouts.NotCompleted); // already answered: nothing sent
        Assert.False(new WorkoutListItemViewModel(vm.Workouts[0], "Γ", isCoach: false).CanComplete);
    }

    [Fact]
    public void The_confirmation_setting_is_on_by_default_and_remembered()
    {
        var prefs = new InMemoryAppPreferences();
        Assert.True(new WorkoutAnswerSettings(prefs).ConfirmAnswers);

        new WorkoutAnswerSettings(prefs).ConfirmAnswers = false;

        Assert.False(new WorkoutAnswerSettings(prefs).ConfirmAnswers);
    }

    // ---- reminders ----

    [Fact]
    public async Task The_coach_reminds_an_open_workout_once_an_hour()
    {
        var w = Add(5);
        var vm = await Profile(Sessions.Coach(_coachId));
        Assert.True(vm.CanRemind);
        Assert.True(vm.Workouts[0].CanBeReminded);

        await vm.RemindWorkoutCommand.ExecuteAsync(vm.Workouts[0]);

        Assert.Equal(new[] { w.Id }, _workouts.Reminded);
        Assert.Contains("Στάλθηκε υπενθύμιση", vm.InfoMessage);
        Assert.False(vm.Workouts[0].CanBeReminded); // within the hour
        Assert.True(vm.Workouts[0].ShowReminderSent);

        await vm.RemindWorkoutCommand.ExecuteAsync(vm.Workouts[0]);
        Assert.Single(_workouts.Reminded);
    }

    [Fact]
    public async Task No_reminders_for_answered_workouts_or_athletes_without_a_login()
    {
        Add(5, setup: w => w.CompletedAt = DateTimeOffset.UtcNow);
        var vm = await Profile(Sessions.Coach(_coachId));

        await vm.RemindWorkoutCommand.ExecuteAsync(vm.Workouts[0]);
        Assert.Empty(_workouts.Reminded);
        Assert.False(vm.Workouts[0].CanBeReminded);

        _athletes.Items[0].AuthUserId = null;
        var noLogin = await Profile(Sessions.Coach(_coachId));
        Assert.False(noLogin.CanRemind);
    }

    [Fact]
    public async Task A_refused_reminder_explains_why()
    {
        Add(5);
        _workouts.RemindRefusal = ReminderRefusal.TooSoon;
        var vm = await Profile(Sessions.Coach(_coachId));

        await vm.RemindWorkoutCommand.ExecuteAsync(vm.Workouts[0]);

        Assert.Equal("Έχει σταλεί υπενθύμιση την τελευταία ώρα. Δοκίμασε ξανά αργότερα.", vm.ErrorMessage);
    }

    [Fact]
    public void The_athlete_never_gets_the_bell()
    {
        var item = new WorkoutListItemViewModel(new WorkoutProgram { TargetDate = new DateOnly(2026, 10, 5) }, "Γ",
            isCoach: false, athleteHasLogin: true);

        Assert.False(item.CanRemind);
        Assert.True(item.CanComplete);
    }

    // ---- notifications ----

    [Fact]
    public async Task Opening_from_a_notification_opens_that_workouts_month()
    {
        Add(20, month: 10);
        var old = Add(3, month: 8);

        var vm = await Profile(Sessions.Athlete(_athleteId, _coachId), focus: old.Id);

        Assert.Same(old, vm.FocusedWorkout);
        Assert.True(vm.WorkoutGroups.Single(g => g.Month == 8).IsExpanded);
        Assert.True(vm.WorkoutGroups.Single(g => g.Month == 8).Weeks.Single().IsExpanded); // its week too
        Assert.True(vm.WorkoutGroups.Single(g => g.Month == 10).IsExpanded); // the newest stays open
    }

    [Theory]
    [InlineData("workout_reminder")]
    [InlineData("workout_not_completed")]
    [InlineData("workout_completed")]
    public void A_tapped_push_of_the_new_types_is_handled(string type)
    {
        var id = Guid.NewGuid();
        NotificationNavigation.RequestFor(type, id.ToString());

        var request = NotificationNavigation.TakePending();

        Assert.NotNull(request);
        Assert.Equal(new[] { id }, request.NotificationIds);
    }

    [Fact]
    public void Notification_times_read_naturally()
    {
        var now = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal("πριν 5 λεπτά", TimeText.Stamp(now.AddMinutes(-5), now));
        Assert.Equal("πριν 3 ώρες", TimeText.Stamp(now.AddHours(-3), now));
        var older = now.AddDays(-2);
        Assert.Equal(older.ToLocalTime().ToString("dd/MM HH:mm"), TimeText.Stamp(older, now));
    }
}
