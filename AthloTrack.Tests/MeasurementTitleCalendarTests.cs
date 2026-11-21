using AthloTrack.Core.Auth;
using AthloTrack.Core.Models;
using AthloTrack.Core.Supabase;
using AthloTrack.ViewModels;

namespace AthloTrack.Tests;

/// <summary>
/// 021: a measurement's optional title («Τεστ Cooper»), measurements in Ημερολόγιο (several a
/// day, opening the profile at them) and the saved sign-in's time limit.
/// </summary>
public sealed class MeasurementTitleCalendarTests
{
    private readonly Guid _coachId = Guid.NewGuid();
    private readonly Athlete _maria = new() { Id = Guid.NewGuid(), FullName = "Μαρία Κωνσταντίνου" };
    private readonly FakeMeasurements _measurements = new();
    private readonly FakeMeasurementFields _fields = new();

    private async Task<AddMeasurementViewModel> Form()
    {
        var vm = new AddMeasurementViewModel(_maria.Id, _measurements, Sessions.Coach(_coachId), _fields);
        await vm.FieldsLoaded;
        return vm;
    }

    [Fact]
    public async Task The_title_is_saved_trimmed_and_shown_instead_of_Μέτρηση()
    {
        var vm = await Form();
        vm.AddCustomSectionCommand.Execute(null);
        vm.MeasurementTitle = "  Τεστ Cooper ";
        vm.WeightKg = "57.6";

        await vm.SaveCommand.ExecuteAsync(null);

        var m = Assert.Single(_measurements.Items);
        Assert.Equal("Τεστ Cooper", m.Title);
        Assert.Equal("Τεστ Cooper", m.DisplayTitle);
        Assert.Equal("Μέτρηση", new Measurement().DisplayTitle);
    }

    [Fact]
    public async Task A_blank_title_or_a_closed_section_saves_no_title()
    {
        var vm = await Form();
        vm.AddCustomSectionCommand.Execute(null);
        vm.MeasurementTitle = "Άλμα";
        vm.RemoveCustomSectionCommand.Execute(null); // × on «Άλλες μετρήσεις» takes the title too
        Assert.Equal(string.Empty, vm.MeasurementTitle);
        vm.WeightKg = "70";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Null(Assert.Single(_measurements.Items).Title);

        var blank = await Form();
        blank.AddCustomSectionCommand.Execute(null);
        blank.MeasurementTitle = "   ";
        blank.WeightKg = "71";
        await blank.SaveCommand.ExecuteAsync(null);
        Assert.Null(_measurements.Items[1].Title);
    }

    [Fact]
    public async Task A_title_alone_is_not_a_measurement()
    {
        var vm = await Form();
        vm.AddCustomSectionCommand.Execute(null);
        vm.MeasurementTitle = "Τεστ Cooper";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_measurements.Items);
        Assert.Equal(AddMeasurementViewModel.NothingText, vm.ErrorMessage);
    }

    [Fact]
    public async Task Editing_opens_the_section_with_the_title_and_can_change_it()
    {
        var existing = new Measurement { Id = Guid.NewGuid(), AthleteId = _maria.Id, MeasuredAt = new DateOnly(2026, 11, 21), WeightKg = 58, Title = "Τεστ Cooper" };
        _measurements.Items.Add(existing);
        var vm = await Form();

        vm.BeginEdit(existing);
        Assert.True(vm.ShowCustom);
        Assert.Equal("Τεστ Cooper", vm.MeasurementTitle);

        vm.MeasurementTitle = "Τεστ Cooper (2ο)";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Τεστ Cooper (2ο)", existing.Title);
    }

    [Fact]
    public async Task A_title_longer_than_80_characters_is_refused()
    {
        var vm = await Form();
        vm.AddCustomSectionCommand.Execute(null);
        vm.MeasurementTitle = new string('α', 81);
        vm.WeightKg = "70";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_measurements.Items);
    }

    // ---- Ημερολόγιο ----

    private Measurement AddMeasurement(DateOnly date, int minute, string? title = null, decimal? weight = 57.6m)
    {
        var m = new Measurement
        {
            Id = Guid.NewGuid(), AthleteId = _maria.Id, MeasuredAt = date, WeightKg = weight, Title = title,
            CreatedAt = new DateTimeOffset(2026, 11, 21, 10, minute, 0, TimeSpan.Zero),
        };
        _measurements.Items.Add(m);
        return m;
    }

    private async Task<CalendarViewModel> Calendar(FakeWorkouts? workouts = null)
    {
        var athletes = new FakeAthletes();
        athletes.Items.Add(_maria);
        var vm = new CalendarViewModel(workouts ?? new FakeWorkouts(), athletes, new FakeAvatars(), _measurements, _fields);
        await vm.LoadAsync();
        return vm;
    }

    [Fact]
    public async Task Several_measurements_a_day_get_one_ruler_with_a_count()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        // Added out of order: the day lists them by when they were added.
        AddMeasurement(today, minute: 30, title: "Βάρος & λιπομέτρηση");
        AddMeasurement(today, minute: 5, title: "Τεστ Cooper");
        var other = today.Day > 1 ? today.AddDays(-1) : today.AddDays(1); // same month
        AddMeasurement(other, minute: 1);

        var vm = await Calendar();
        var day = vm.Days.Single(d => d.Date == today);

        Assert.True(day.HasMeasurements);
        Assert.True(day.HasManyMeasurements);
        Assert.Equal("2", day.MeasurementCountText);
        Assert.False(day.HasWorkouts); // measurements are not athletes' photos
        Assert.Equal(["Τεστ Cooper", "Βάρος & λιπομέτρηση"], day.Measurements.Select(m => m.Heading));

        var yesterday = vm.Days.Single(d => d.Date == other);
        Assert.True(yesterday.HasMeasurements);
        Assert.False(yesterday.HasManyMeasurements);
        Assert.Equal("Μέτρηση", yesterday.Measurements[0].Heading); // no title
    }

    [Fact]
    public async Task The_day_lists_workouts_then_measurements_and_a_tap_opens_the_measurement()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var workouts = new FakeWorkouts();
        workouts.Items.Add(new WorkoutProgram { Id = Guid.NewGuid(), AthleteId = _maria.Id, Content = "τρέξιμο", TargetDate = today });
        var cooper = AddMeasurement(today, minute: 1, title: "Τεστ Cooper");

        var vm = await Calendar(workouts);
        vm.SelectDayCommand.Execute(vm.Days.Single(d => d.Date == today));

        Assert.Equal(2, vm.AgendaEntries.Count);
        Assert.True(vm.AgendaEntries[0].IsWorkout);
        var row = vm.AgendaEntries[1];
        Assert.True(row.IsMeasurement);
        Assert.Equal("ΜΕΤΡΗΣΗ · Μαρία Κωνσταντίνου", row.KindText);
        Assert.Equal("Τεστ Cooper", row.Heading);
        Assert.Equal($"{57.6m} kg", row.Content);

        (Guid, Guid)? opened = null;
        Guid? openedWorkout = Guid.Empty;
        vm.OpenMeasurementRequested += (a, m) => opened = (a, m);
        vm.OpenWorkoutRequested += (_, w) => openedWorkout = w;
        vm.OpenEntryCommand.Execute(row);
        Assert.Equal((_maria.Id, cooper.Id), opened);
        Assert.Equal(Guid.Empty, openedWorkout); // not taken for a workout
    }

    [Fact]
    public async Task The_month_agenda_includes_measurements()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        AddMeasurement(today, minute: 1, title: "Τεστ Cooper");

        var vm = await Calendar();

        Assert.Single(vm.AgendaEntries);
        Assert.StartsWith("Προπονήσεις και μετρήσεις — ", vm.AgendaTitle);
        Assert.False(vm.HasNoAgendaEntries);
    }

    [Fact]
    public void The_summary_lists_the_values_or_the_notes()
    {
        var field = new MeasurementField { Id = Guid.NewGuid(), CoachId = _coachId, Name = "Απόσταση", Unit = "m" };
        var full = new Measurement { WeightKg = 57.6m, FatMassWt = 20.4m, Extra = { [field.Id] = 2450 } };
        // The device's number format, like the profile («57,6» in Greek).
        Assert.Equal($"{57.6m} kg · Fat Mass/WT {20.4m} · Απόσταση: 2450 m", CalendarViewModel.Summary(full, [field]));

        var notesOnly = new Measurement { Notes = "Καλή αντοχή" };
        Assert.Equal("Καλή αντοχή", CalendarViewModel.Summary(notesOnly, []));
    }

    [Fact]
    public async Task Without_migration_021_the_calendar_still_shows_workouts()
    {
        var workouts = new FakeWorkouts();
        workouts.Items.Add(new WorkoutProgram { Id = Guid.NewGuid(), AthleteId = _maria.Id, Content = "τρέξιμο", TargetDate = DateOnly.FromDateTime(DateTime.Today) });
        var athletes = new FakeAthletes();
        athletes.Items.Add(_maria);

        var vm = new CalendarViewModel(workouts, athletes, new FakeAvatars(), new FailingMeasurements());
        await vm.LoadAsync();

        Assert.Null(vm.ErrorMessage);
        Assert.Single(vm.AgendaEntries);
    }

    private sealed class FailingMeasurements : AthloTrack.Core.Data.IMeasurementRepository
    {
        public Task<IReadOnlyList<Measurement>> GetForAthleteAsync(Guid athleteId) => throw new InvalidOperationException();
        public Task<IReadOnlyList<Measurement>> GetAllAsync() => throw new InvalidOperationException("column title does not exist");
        public Task<Measurement> AddAsync(Measurement measurement) => throw new InvalidOperationException();
        public Task<Measurement> UpdateAsync(Measurement measurement) => throw new InvalidOperationException();
        public Task DeleteAsync(Guid id) => throw new InvalidOperationException();
    }

    [Fact]
    public async Task The_profile_opens_at_the_tapped_measurement()
    {
        var athletes = new FakeAthletes();
        athletes.Items.Add(_maria);
        var old = AddMeasurement(new DateOnly(2025, 3, 2), minute: 1, title: "Τεστ Cooper");
        AddMeasurement(DateOnly.FromDateTime(DateTime.Today), minute: 2);

        var vm = new AthleteProfileViewModel(_maria.Id, athletes, _measurements, new FakeWorkouts(),
            Sessions.Coach(_coachId), new FakeAvatars(), focusMeasurementId: old.Id);
        await vm.LoadAsync();

        Assert.Same(old, vm.FocusedMeasurement);
        Assert.True(vm.MeasurementGroups.Single(g => g.Items.Contains(old)).IsExpanded); // its old month opened
    }

    // ---- the saved sign-in's time limit ----

    private sealed class StuckAuth : IAuthService
    {
        public readonly TaskCompletionSource<AuthResult> Restore = new();
        public Task<AuthResult> SignInAsync(string email, string password) => Task.FromResult(AuthResult.Fail("x"));
        public Task<AuthResult> RestoreSessionAsync() => Restore.Task;
        public Task<AuthResult> SignUpAsync(string email, string password) => Task.FromResult(AuthResult.Fail("x"));
        public Task<AuthResult> UpdatePasswordAsync(string newPassword) => Task.FromResult(AuthResult.Fail("x"));
        public Task SignOutAsync() => Task.CompletedTask;
    }

    private sealed class CoachProfile : ISessionInitializer
    {
        public Task<bool> InitializeAsync(UserRole role, Guid authUserId) => Task.FromResult(role == UserRole.Coach);
    }

    [Fact]
    public async Task A_stuck_saved_sign_in_gives_the_login_form_back()
    {
        var auth = new StuckAuth();
        var vm = new LoginViewModel(auth, new CoachProfile(), new SupabaseConfig { Url = "https://x.supabase.co", AnonKey = "k" },
            new InMemoryAppPreferences()) { RestoreTimeout = TimeSpan.FromMilliseconds(50) };
        UserRole? signedIn = null;
        vm.LoginSucceeded += r => signedIn = r;

        await vm.TryRestoreSessionAsync();

        Assert.True(vm.IsReady);
        Assert.False(vm.IsBusy); // the form's buttons work again

        // The network wakes up later: the user is on the form by now, so nothing jumps.
        auth.Restore.SetResult(AuthResult.Ok(Guid.NewGuid(), "a", "r"));
        await Task.Delay(20);
        Assert.Null(signedIn);
    }

    [Fact]
    public async Task A_quick_saved_sign_in_still_signs_in()
    {
        var auth = new StuckAuth();
        auth.Restore.SetResult(AuthResult.Ok(Guid.NewGuid(), "a", "r"));
        var vm = new LoginViewModel(auth, new CoachProfile(), new SupabaseConfig { Url = "https://x.supabase.co", AnonKey = "k" },
            new InMemoryAppPreferences());
        UserRole? signedIn = null;
        vm.LoginSucceeded += r => signedIn = r;

        await vm.TryRestoreSessionAsync();

        Assert.Equal(UserRole.Coach, signedIn);
        Assert.True(vm.LastLoginWasRestored);
    }
}
