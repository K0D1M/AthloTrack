using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;
using AthloTrack.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

/// <summary>
/// Προπονήσεις — all workout programs across athletes: search, filter chips, sorting (remembered),
/// grouped by day, athlete or status. The athlete's next open workout is shown on its own card.
/// </summary>
public partial class WorkoutsViewModel : ViewModelBase
{
    internal const string FilterKey = "workouts.filter";
    internal const string SortKey = "workouts.sort";

    private static readonly string[] Days = ["Κυριακή", "Δευτέρα", "Τρίτη", "Τετάρτη", "Πέμπτη", "Παρασκευή", "Σάββατο"];
    private static readonly StringComparer GreekNames = StringComparer.Create(new CultureInfo("el-GR"), ignoreCase: true);

    private readonly IWorkoutRepository _workouts;
    private readonly IAthleteRepository _athletes;
    private readonly SessionState _session;
    private readonly AthloTrack.Services.WorkoutAnswerSettings? _answerSettings;
    private readonly IAppPreferences? _preferences;

    public WorkoutsViewModel(IWorkoutRepository workouts, IAthleteRepository athletes, SessionState session,
        AthloTrack.Services.WorkoutAnswerSettings? answerSettings = null, IAppPreferences? preferences = null)
    {
        _workouts = workouts;
        _athletes = athletes;
        _session = session;
        _answerSettings = answerSettings;
        _preferences = preferences;

        Filters =
        [
            new(WorkoutFilter.All, "Όλες"), new(WorkoutFilter.Today, "Σήμερα"), new(WorkoutFilter.Upcoming, "Επερχόμενες"),
            new(WorkoutFilter.Open, "Εκκρεμούν"), new(WorkoutFilter.Answered, "Με απάντηση"),
        ];
        var sorts = new List<WorkoutSortOption>
        {
            new(WorkoutSort.Smart, "Πρώτα τα σημερινά", "Σήμερα και επόμενες, μετά οι προηγούμενες", "Σήμερα ↓"),
            new(WorkoutSort.Newest, "Ημερομηνία: νεότερες πρώτα", "Από την πιο μακρινή προς τα πίσω", "Νεότερες"),
            new(WorkoutSort.Oldest, "Ημερομηνία: παλαιότερες πρώτα", "Από την πιο παλιά", "Παλαιότερες"),
        };
        if (session.IsCoach) sorts.Add(new(WorkoutSort.Athlete, "Αθλητής (Α–Ω)", "Ομαδοποίηση ανά αθλητή", "Αθλητής"));
        sorts.Add(new(WorkoutSort.Status, "Κατάσταση", "Εκπρόθεσμες και εκκρεμείς πρώτα", "Κατάσταση"));
        SortOptions = sorts;

        // The last choices, as the user left them.
        _filter = Enum.TryParse<WorkoutFilter>(_preferences?.Get(FilterKey), out var f) ? f : WorkoutFilter.All;
        _sort = Enum.TryParse<WorkoutSort>(_preferences?.Get(SortKey), out var s) && sorts.Any(o => o.Sort == s) ? s : WorkoutSort.Smart;
        MarkChoices();
        _ = LoadAsync();
    }

    /// <summary>The coach picked an athlete for «Νέο ασκησιολόγιο».</summary>
    public event Action<Guid, string>? AddWorkoutRequested;

    /// <summary>The coach tapped the pencil on a workout.</summary>
    public event Action<WorkoutProgram, string>? EditWorkoutRequested;

    public bool IsCoach => _session.IsCoach;

    /// <summary>The date the list is grouped against; a test can fix it.</summary>
    public Func<DateOnly> Today { get; set; } = () => DateOnly.FromDateTime(DateTime.Today);

    public string SearchWatermark => IsCoach ? "Αναζήτηση αθλητή ή άσκησης" : "Αναζήτηση άσκησης";

    public IReadOnlyList<WorkoutFilterOption> Filters { get; }
    public IReadOnlyList<WorkoutSortOption> SortOptions { get; }

    private WorkoutFilter _filter;
    private WorkoutSort _sort;

    public WorkoutFilter Filter => _filter;
    public WorkoutSort Sort => _sort;

    /// <summary>The sort button's text: «Σήμερα ↓», «Νεότερες», …</summary>
    public string SortShort => SortOptions.First(o => o.Sort == _sort).Short;

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    partial void OnSearchTextChanged(string value) => Refresh();

    [ObservableProperty]
    public partial bool IsSortOpen { get; set; }

    [ObservableProperty]
    public partial bool IsAthletePickerOpen { get; set; }

    /// <summary>The list as shown: headings with their workouts.</summary>
    public ObservableCollection<WorkoutGroup> Groups { get; } = new();

    /// <summary>The coach's athletes, A–Ω, for «Νέο ασκησιολόγιο».</summary>
    public ObservableCollection<AthleteChoice> Athletes { get; } = new();

    [ObservableProperty]
    public partial string SummaryText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOverdue))]
    public partial string OverdueText { get; set; } = string.Empty;

    public bool HasOverdue => OverdueText.Length > 0;

    /// <summary>There are workouts, but none match the search and chip.</summary>
    [ObservableProperty]
    public partial bool IsFilteredEmpty { get; set; }

    /// <summary>Athlete: the nearest open workout from today on, on its own card above the list.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNext))]
    public partial WorkoutListItemViewModel? Next { get; set; }

    public bool HasNext => Next is not null;

    /// <summary>«Σήμερα, Τρί 28/10» on the next-workout card.</summary>
    [ObservableProperty]
    public partial string NextDayText { get; set; } = string.Empty;

    /// <summary>«Σε 6 ώρες · ο προπονητής θα είναι παρών».</summary>
    [ObservableProperty]
    public partial string NextHint { get; set; } = string.Empty;

    [RelayCommand]
    private void PickFilter(WorkoutFilterOption? option)
    {
        if (option is null) return;
        _filter = option.Filter;
        _preferences?.Set(FilterKey, _filter.ToString());
        MarkChoices();
        Refresh();
    }

    [RelayCommand]
    private void OpenSort() => IsSortOpen = true;

    [RelayCommand]
    private void CloseSort() => IsSortOpen = false;

    [RelayCommand]
    private void PickSort(WorkoutSortOption? option)
    {
        IsSortOpen = false;
        if (option is null) return;
        _sort = option.Sort;
        _preferences?.Set(SortKey, _sort.ToString());
        MarkChoices();
        Refresh();
    }

    /// <summary>«+»: choose the athlete first, then the form opens.</summary>
    [RelayCommand]
    private void AddWorkout()
    {
        if (!IsCoach) return;
        if (Athletes.Count == 1)
        {
            AddWorkoutRequested?.Invoke(Athletes[0].Id, Athletes[0].Name);
            return;
        }
        IsAthletePickerOpen = true;
    }

    [RelayCommand]
    private void CloseAthletePicker() => IsAthletePickerOpen = false;

    [RelayCommand]
    private void PickAthlete(AthleteChoice? athlete)
    {
        IsAthletePickerOpen = false;
        if (athlete is not null) AddWorkoutRequested?.Invoke(athlete.Id, athlete.Name);
    }

    [RelayCommand]
    private void EditWorkout(WorkoutListItemViewModel? item)
    {
        if (item is not null && item.CanEdit) EditWorkoutRequested?.Invoke(item.Program, item.AthleteName);
    }

    private void MarkChoices()
    {
        foreach (var f in Filters) f.IsSelected = f.Filter == _filter;
        foreach (var s in SortOptions) s.IsSelected = s.Sort == _sort;
        OnPropertyChanged(nameof(Filter));
        OnPropertyChanged(nameof(Sort));
        OnPropertyChanged(nameof(SortShort));
    }

    private static bool Matches(WorkoutFilter filter, WorkoutListItemViewModel w, DateOnly today) => filter switch
    {
        WorkoutFilter.Today => w.Program.TargetDate == today,
        WorkoutFilter.Upcoming => w.Program.TargetDate > today && !w.Program.IsCancelled && !w.Program.IsDeclined,
        WorkoutFilter.Open => !w.Program.IsClosed,
        WorkoutFilter.Answered => w.Program.IsAnswered,
        _ => true,
    };

    private static string Ddmm(DateOnly d) => d.ToString("dd/MM", CultureInfo.InvariantCulture);

    private static string DayTitle(DateOnly date, DateOnly today) => (date.DayNumber - today.DayNumber) switch
    {
        0 => "Σήμερα",
        1 => "Αύριο",
        -1 => "Χθες",
        _ => Days[(int)date.DayOfWeek],
    };

    /// <summary>Rebuilds the shown list from the loaded workouts: search, chip, sort, headings.</summary>
    private void Refresh()
    {
        var today = Today();
        var q = SearchText.Trim();
        var found = WorkoutPrograms.Where(w => q.Length == 0
            || w.AthleteName.Contains(q, StringComparison.CurrentCultureIgnoreCase)
            || w.Program.Content.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();

        foreach (var f in Filters) f.Count = found.Count(w => Matches(f.Filter, w, today));

        // The athlete's next workout gets its own card, while the whole list is shown.
        var next = IsCoach || _filter != WorkoutFilter.All || q.Length > 0
            ? null
            : WorkoutPrograms.Where(w => !w.Program.IsClosed && w.Program.TargetDate >= today)
                .OrderBy(w => w.Program.TargetDate).ThenBy(w => w.Program.StartTime ?? TimeOnly.MaxValue).FirstOrDefault();
        Next = next;
        if (next is not null)
        {
            var d = next.Program.TargetDate;
            NextDayText = d.DayNumber - today.DayNumber is 0 or 1
                ? $"{DayTitle(d, today)}, {next.DayText}"
                : next.DayText;
            NextHint = NextHintOf(next.Program, today);
        }

        var shown = found.Where(w => Matches(_filter, w, today) && w != next).ToList();
        static int ByTime(WorkoutListItemViewModel a, WorkoutListItemViewModel b) =>
            (a.Program.StartTime ?? TimeOnly.MaxValue).CompareTo(b.Program.StartTime ?? TimeOnly.MaxValue);
        Comparison<WorkoutListItemViewModel> order = _sort switch
        {
            WorkoutSort.Newest => (a, b) => b.Program.TargetDate.CompareTo(a.Program.TargetDate) is var c and not 0 ? c : ByTime(a, b),
            WorkoutSort.Oldest => (a, b) => a.Program.TargetDate.CompareTo(b.Program.TargetDate) is var c and not 0 ? c : ByTime(a, b),
            WorkoutSort.Athlete => (a, b) => GreekNames.Compare(a.AthleteName, b.AthleteName) is var c and not 0 ? c
                : b.Program.TargetDate.CompareTo(a.Program.TargetDate),
            WorkoutSort.Status => (a, b) => a.StatusRank.CompareTo(b.StatusRank) is var c and not 0 ? c
                : a.Program.TargetDate.CompareTo(b.Program.TargetDate),
            // Today and the coming days (nearest first), then the past ones (latest first).
            _ => (a, b) =>
            {
                bool fa = a.Program.TargetDate >= today, fb = b.Program.TargetDate >= today;
                if (fa != fb) return fa ? -1 : 1;
                var c = fa ? a.Program.TargetDate.CompareTo(b.Program.TargetDate) : b.Program.TargetDate.CompareTo(a.Program.TargetDate);
                return c != 0 ? c : ByTime(a, b);
            },
        };
        shown.Sort(order);

        Groups.Clear();
        string? key = null;
        List<WorkoutListItemViewModel>? items = null;
        foreach (var w in shown)
        {
            var date = w.Program.TargetDate;
            var (k, title, sub, isToday) = _sort switch
            {
                WorkoutSort.Athlete => (w.AthleteName, w.AthleteName, string.Empty, false),
                WorkoutSort.Status => (w.StatusText, w.StatusText, string.Empty, false),
                _ => (date.ToString("O"), DayTitle(date, today),
                    (Math.Abs(date.DayNumber - today.DayNumber) <= 1 ? Days[(int)date.DayOfWeek] + " " : string.Empty)
                    + (date.Year == today.Year ? Ddmm(date) : date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
                    date == today),
            };
            if (k != key)
            {
                key = k;
                items = new List<WorkoutListItemViewModel>();
                Groups.Add(new WorkoutGroup(title, sub, isToday, items));
            }
            items!.Add(w);
        }

        var todayCount = WorkoutPrograms.Count(w => w.Program.TargetDate == today && !w.Program.IsCancelled);
        SummaryText = $"Σήμερα: {todayCount} {(todayCount == 1 ? "προπόνηση" : "προπονήσεις")}";
        var overdue = WorkoutPrograms.Count(w => w.IsOverdue);
        OverdueText = overdue == 0 ? string.Empty : $"{overdue} {(overdue == 1 ? "εκπρόθεσμη" : "εκπρόθεσμες")}";
        IsFilteredEmpty = WorkoutPrograms.Count > 0 && Groups.Count == 0 && next is null;
    }

    /// <summary>«Σε 6 ώρες», «Αύριο», «Σε 3 ημέρες», with the coach's presence.</summary>
    private static string NextHintOf(WorkoutProgram p, DateOnly today)
    {
        var days = p.TargetDate.DayNumber - today.DayNumber;
        string when;
        if (days == 0 && p.StartTime is { } t)
        {
            var left = p.TargetDate.ToDateTime(t) - DateTime.Now;
            when = left.TotalMinutes < 1 ? "Τώρα"
                : left.TotalHours < 1 ? $"Σε {(int)Math.Ceiling(left.TotalMinutes)} λεπτά"
                : (int)Math.Round(left.TotalHours) is var h && h == 1 ? "Σε 1 ώρα" : $"Σε {(int)Math.Round(left.TotalHours)} ώρες";
        }
        else
        {
            when = days switch { 0 => "Σήμερα", 1 => "Αύριο", _ => $"Σε {days} ημέρες" };
        }
        return p.CoachPresent switch
        {
            true => $"{when} · ο προπονητής θα είναι παρών",
            false => $"{when} · χωρίς τον προπονητή",
            _ => when,
        };
    }

    /// <summary>«Σίγουρα;» before an answer (unless turned off in Ρυθμίσεις).</summary>
    public ConfirmPrompt Confirm { get; } = new();

    [ObservableProperty]
    public partial string? InfoMessage { get; set; }

    public ObservableCollection<WorkoutListItemViewModel> WorkoutPrograms { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSkeleton))]
    public partial bool IsLoading { get; set; }

    /// <summary>
    /// The first load is running: show placeholders shaped like the content. Later reloads keep
    /// the content and only show the loading line.
    /// </summary>
    public bool ShowSkeleton => IsLoading && !_hasLoaded;

    private bool _hasLoaded;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>The athlete marks an open workout done; the DB then notifies the coach.</summary>
    [RelayCommand]
    private Task CompleteWorkoutAsync(WorkoutListItemViewModel? item) =>
        AnswerAsync(item, i => i.CanComplete, "Ολοκληρώθηκε", id => _workouts.MarkCompletedAsync(id));

    /// <summary>The athlete answers «Δεν ολοκληρώθηκε»; the DB then notifies the coach.</summary>
    [RelayCommand]
    private Task NotCompleteWorkoutAsync(WorkoutListItemViewModel? item) =>
        AnswerAsync(item, i => i.CanComplete, "Δεν ολοκληρώθηκε", id => _workouts.MarkNotCompletedAsync(id));

    /// <summary>The athlete's «Θα το κάνω» on a new workout; the DB then notifies the coach.</summary>
    [RelayCommand]
    private Task AcceptWorkoutAsync(WorkoutListItemViewModel? item) =>
        AnswerAsync(item, i => i.CanAccept, "Θα το κάνω", id => _workouts.AcceptAsync(id));

    /// <summary>«Δεν θα το κάνω» (final), always asked: it carries the optional reason.</summary>
    [RelayCommand]
    private void DeclineWorkout(WorkoutListItemViewModel? item)
    {
        if (item is null || !item.CanAccept) return;
        Confirm.AskWithReason(AthleteProfileViewModel.DeclineText, "Δεν θα το κάνω", async reason =>
        {
            try
            {
                await _workouts.DeclineAsync(item.Program.Id, reason);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        });
    }

    /// <summary>The coach cancels a workout (it stays, dimmed); the athlete is notified.</summary>
    [RelayCommand]
    private void CancelWorkout(WorkoutListItemViewModel? item)
    {
        if (item is null || !item.CanCancel) return;
        Confirm.Ask(AthleteProfileViewModel.CancelText(item.Program.TargetDate), "Ναι, ακύρωση",
            () => CancelOrRestoreAsync(() => _workouts.CancelAsync(item.Program.Id)), danger: true);
    }

    /// <summary>The coach undoes a cancel; the athlete is notified.</summary>
    [RelayCommand]
    private Task RestoreWorkoutAsync(WorkoutListItemViewModel? item) =>
        item is null || !item.CanRestore
            ? Task.CompletedTask
            : CancelOrRestoreAsync(() => _workouts.RestoreAsync(item.Program.Id));

    private async Task CancelOrRestoreAsync(Func<Task> send)
    {
        ErrorMessage = null;
        InfoMessage = null;
        try
        {
            await send();
            await LoadAsync();
        }
        catch (WorkoutCancelException ex)
        {
            ErrorMessage = AthleteProfileViewModel.CancelRefusalText(ex.Reason);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private async Task AnswerAsync(WorkoutListItemViewModel? item, Func<WorkoutListItemViewModel, bool> allowed,
        string button, Func<Guid, Task> send)
    {
        if (item is null || !allowed(item)) return;
        async Task Run()
        {
            try
            {
                await send(item.Program.Id);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        if (_answerSettings?.ConfirmAnswers ?? true)
        {
            Confirm.Ask(AthloTrack.Services.WorkoutAnswerSettings.ConfirmText, button, Run);
            return;
        }
        await Run();
    }

    /// <summary>The coach asks the athlete whether the workout was done (at most once an hour).</summary>
    [RelayCommand]
    private async Task RemindWorkoutAsync(WorkoutListItemViewModel? item)
    {
        if (item is null || !item.CanRemind || !item.RemindEnabled) return;
        ErrorMessage = null;
        InfoMessage = null;
        try
        {
            await _workouts.RemindAsync(item.Program.Id);
            InfoMessage = $"Στάλθηκε υπενθύμιση στον/στην {item.AthleteName} για το ασκησιολόγιο της {item.Program.TargetDate:dd/MM}.";
            await LoadAsync();
        }
        catch (WorkoutReminderException ex)
        {
            ErrorMessage = AthleteProfileViewModel.ReminderText(ex.Reason);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>The athlete has had this workout on screen: the coach gets the read receipt.</summary>
    // Concurrent: several workouts come into view at once; otherwise CanExecute is false while one is saving.
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task WorkoutSeenAsync(WorkoutListItemViewModel? item)
    {
        if (item is null || _session.IsCoach || item.Program.IsRead) return;
        item.Program.ReadAt = DateTimeOffset.UtcNow; // don't send it twice
        await AthleteProfileViewModel.MarkWorkoutReadAsync(_workouts, item.Program.Id);
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var athletes = await _athletes.GetAllAsync();
            var names = new Dictionary<Guid, string>();
            var withLogin = new HashSet<Guid>();
            foreach (var a in athletes)
            {
                names[a.Id] = a.FullName;
                if (a.AuthUserId is not null) withLogin.Add(a.Id);
            }
            Athletes.Clear();
            foreach (var a in athletes.OrderBy(a => a.FullName, GreekNames)) Athletes.Add(new AthleteChoice(a.Id, a.FullName));

            var programs = await _workouts.GetAllAsync();
            var today = Today();
            WorkoutPrograms.Clear();
            foreach (var p in programs)
            {
                var name = names.TryGetValue(p.AthleteId, out var n) ? n : "—";
                WorkoutPrograms.Add(new WorkoutListItemViewModel(p, name, isCoach: _session.IsCoach,
                    athleteHasLogin: withLogin.Contains(p.AthleteId), today: today));
            }
            Refresh();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            _hasLoaded = true;
            IsLoading = false;
        }
    }
}
