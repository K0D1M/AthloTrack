using AthloTrack.Core.Auth;
using AthloTrack.Core.Models;
using AthloTrack.ViewModels;

namespace AthloTrack.Tests;

/// <summary>Προπονήσεις: search, chips, sorting (remembered), day headings, «Εκπρόθεσμο».</summary>
public sealed class WorkoutListTests
{
    private static readonly DateOnly Today = new(2026, 10, 28); // a Wednesday
    private readonly FakeWorkouts _workouts = new();
    private readonly FakeAthletes _athletes = new();
    private readonly Athlete _maria = new() { Id = Guid.NewGuid(), FullName = "Μαρία", AuthUserId = Guid.NewGuid() };
    private readonly Athlete _giorgos = new() { Id = Guid.NewGuid(), FullName = "Γιώργος", AuthUserId = Guid.NewGuid() };

    public WorkoutListTests()
    {
        _athletes.Items.Add(_maria);
        _athletes.Items.Add(_giorgos);
    }

    private WorkoutProgram Add(Athlete a, int day, string content, TimeOnly? time = null, Action<WorkoutProgram>? set = null)
    {
        var w = new WorkoutProgram { Id = Guid.NewGuid(), AthleteId = a.Id, Content = content, TargetDate = Today.AddDays(day), StartTime = time };
        set?.Invoke(w);
        _workouts.Items.Add(w);
        return w;
    }

    private async Task<WorkoutsViewModel> LoadAsync(SessionState session, IAppPreferences? prefs = null)
    {
        var vm = new WorkoutsViewModel(_workouts, _athletes, session, preferences: prefs) { Today = () => Today };
        await vm.LoadAsync();
        return vm;
    }

    private static string[] Titles(WorkoutsViewModel vm) => vm.Groups.Select(g => g.Title).ToArray();

    [Fact]
    public async Task Today_and_the_coming_days_come_first_then_the_past_latest_first()
    {
        Add(_maria, -3, "παλιό");
        Add(_maria, 1, "αύριο");
        Add(_giorgos, 0, "βράδυ", new TimeOnly(19, 0));
        Add(_maria, 0, "πρωί", new TimeOnly(7, 0));
        Add(_giorgos, -1, "χθες");

        var vm = await LoadAsync(Sessions.Coach(Guid.NewGuid()));

        Assert.Equal(new[] { "Σήμερα", "Αύριο", "Χθες", "Κυριακή" }, Titles(vm));
        Assert.Equal(new[] { "πρωί", "βράδυ" }, vm.Groups[0].Items.Select(i => i.Content));
        Assert.True(vm.Groups[0].IsToday);
        Assert.Equal("Τετάρτη 28/10", vm.Groups[0].Subtitle);
        Assert.Equal("Σήμερα: 2 προπονήσεις", vm.SummaryText);
    }

    [Fact]
    public async Task An_open_workout_from_a_past_day_is_overdue()
    {
        Add(_maria, -2, "χωρίς απάντηση");
        Add(_maria, -2, "ολοκληρώθηκε", set: w => { w.AcceptedAt = DateTimeOffset.UtcNow; w.CompletedAt = DateTimeOffset.UtcNow; });
        Add(_maria, -2, "ακυρώθηκε", set: w => w.CancelledAt = DateTimeOffset.UtcNow);

        var vm = await LoadAsync(Sessions.Coach(Guid.NewGuid()));
        var items = vm.WorkoutPrograms.ToDictionary(i => i.Content);

        Assert.True(items["χωρίς απάντηση"].IsOverdue);
        Assert.Equal("Εκπρόθεσμο", items["χωρίς απάντηση"].StatusText);
        Assert.Equal("Ολοκληρώθηκε", items["ολοκληρώθηκε"].StatusText);
        Assert.Equal("Ακυρώθηκε", items["ακυρώθηκε"].StatusText);
        Assert.False(items["ακυρώθηκε"].IsOverdue);
        Assert.Equal("1 εκπρόθεσμη", vm.OverdueText);
    }

    [Fact]
    public async Task Chips_count_and_filter_and_search_narrows_both()
    {
        Add(_maria, 0, "Καθίσματα");
        Add(_maria, 2, "Τρέξιμο");
        Add(_giorgos, -1, "Τρέξιμο χαλαρό", set: w => { w.AcceptedAt = DateTimeOffset.UtcNow; w.CompletedAt = DateTimeOffset.UtcNow; });

        var vm = await LoadAsync(Sessions.Coach(Guid.NewGuid()));
        int Count(WorkoutFilter f) => vm.Filters.Single(o => o.Filter == f).Count;

        Assert.Equal(3, Count(WorkoutFilter.All));
        Assert.Equal(1, Count(WorkoutFilter.Today));
        Assert.Equal(1, Count(WorkoutFilter.Upcoming));
        Assert.Equal(2, Count(WorkoutFilter.Open));
        Assert.Equal(1, Count(WorkoutFilter.Answered));

        vm.PickFilterCommand.Execute(vm.Filters.Single(o => o.Filter == WorkoutFilter.Open));
        Assert.Equal(new[] { "Καθίσματα", "Τρέξιμο" }, vm.Groups.SelectMany(g => g.Items).Select(i => i.Content));

        vm.SearchText = "τρέξ";
        Assert.Equal(new[] { "Τρέξιμο" }, vm.Groups.SelectMany(g => g.Items).Select(i => i.Content));
        Assert.Equal(2, Count(WorkoutFilter.All));

        vm.SearchText = "γιώργ"; // the athlete's name
        Assert.True(vm.IsFilteredEmpty); // his only workout is answered, the chip is «Εκκρεμούν»
    }

    [Fact]
    public async Task By_athlete_groups_by_name_and_the_choice_is_remembered()
    {
        Add(_maria, 0, "α");
        Add(_giorgos, 1, "β");
        var prefs = new InMemoryAppPreferences();
        var vm = await LoadAsync(Sessions.Coach(Guid.NewGuid()), prefs);

        vm.OpenSortCommand.Execute(null);
        vm.PickSortCommand.Execute(vm.SortOptions.Single(o => o.Sort == WorkoutSort.Athlete));

        Assert.False(vm.IsSortOpen);
        Assert.Equal(new[] { "Γιώργος", "Μαρία" }, Titles(vm));
        Assert.Equal("Αθλητής", vm.SortShort);

        var reopened = await LoadAsync(Sessions.Coach(Guid.NewGuid()), prefs);
        Assert.Equal(WorkoutSort.Athlete, reopened.Sort);
        Assert.True(reopened.SortOptions.Single(o => o.Sort == WorkoutSort.Athlete).IsSelected);
    }

    [Fact]
    public async Task The_athlete_sees_the_next_open_workout_on_its_own_card()
    {
        Add(_maria, 2, "μεθαύριο");
        Add(_maria, 0, "σήμερα", new TimeOnly(23, 59));
        Add(_maria, -1, "χθες", set: w => { w.AcceptedAt = DateTimeOffset.UtcNow; w.CompletedAt = DateTimeOffset.UtcNow; });

        var vm = await LoadAsync(Sessions.Athlete(_maria.Id, Guid.NewGuid()));

        Assert.Equal("σήμερα", vm.Next?.Content);
        Assert.StartsWith("Σήμερα, Τετ 28/10", vm.NextDayText);
        Assert.DoesNotContain(vm.Groups.SelectMany(g => g.Items), i => i.Content == "σήμερα");
        Assert.Equal("Νέο", vm.Next!.StatusText);
        Assert.DoesNotContain(vm.SortOptions, o => o.Sort == WorkoutSort.Athlete); // coach only

        vm.PickFilterCommand.Execute(vm.Filters.Single(o => o.Filter == WorkoutFilter.Today));
        Assert.Null(vm.Next); // a chip shows the plain list
        Assert.Single(vm.Groups.SelectMany(g => g.Items));
    }

    [Fact]
    public async Task Plus_and_the_pencil_ask_for_the_form()
    {
        var w = Add(_maria, 0, "α");
        var vm = await LoadAsync(Sessions.Coach(Guid.NewGuid()));
        (Guid, string)? add = null;
        WorkoutProgram? edited = null;
        vm.AddWorkoutRequested += (id, name) => add = (id, name);
        vm.EditWorkoutRequested += (p, _) => edited = p;

        vm.AddWorkoutCommand.Execute(null);
        Assert.True(vm.IsAthletePickerOpen); // two athletes: choose first
        Assert.Equal(new[] { "Γιώργος", "Μαρία" }, vm.Athletes.Select(a => a.Name));
        vm.PickAthleteCommand.Execute(vm.Athletes[1]);
        Assert.Equal((_maria.Id, "Μαρία"), add);
        Assert.False(vm.IsAthletePickerOpen);

        vm.EditWorkoutCommand.Execute(vm.WorkoutPrograms[0]);
        Assert.Same(w, edited);
    }

    [Theory]
    [InlineData("## Ζέσταμα\n- τρέξιμο", "Ζέσταμα")]
    [InlineData("\n- **Καθίσματα** 5×5", "Καθίσματα 5×5")]
    [InlineData("1. Πιέσεις", "Πιέσεις")]
    public void The_preview_is_the_first_line_without_markup(string content, string preview) =>
        Assert.Equal(preview, WorkoutListItemViewModel.PreviewOf(content));
}
