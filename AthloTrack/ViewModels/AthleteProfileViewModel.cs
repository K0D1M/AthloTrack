using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;
using AthloTrack.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

/// <summary>Progress chart data: one x label per measurement date, one line per metric.</summary>
public sealed record ProgressChart(string[] Labels, System.Collections.Generic.IReadOnlyList<ChartLine> Lines);

/// <summary>A metric over time; null values are measurements without that metric.</summary>
public sealed record ChartLine(string Name, string ColorHex, double?[] Values, double Width = 2, bool Fill = false);

public partial class AthleteProfileViewModel : ViewModelBase
{
    private readonly Guid _athleteId;
    private readonly IAthleteRepository _athletes;
    private readonly IMeasurementRepository _measurements;
    private readonly IWorkoutRepository _workouts;
    private readonly SessionState _session;
    private readonly IAvatarService _avatars;
    private readonly CurrentUserViewModel? _currentUser;

    private Athlete? _athlete;
    private Func<Task>? _pendingConfirm;
    private readonly AthloTrack.Services.WorkoutAnswerSettings? _answerSettings;

    /// <summary>Opened from a notification: the workout to show (its month opened, scrolled to).</summary>
    private Guid? _focusWorkoutId;

    public AthleteProfileViewModel(
        Guid athleteId,
        IAthleteRepository athletes,
        IMeasurementRepository measurements,
        IWorkoutRepository workouts,
        SessionState session,
        IAvatarService avatars,
        CurrentUserViewModel? currentUser = null,
        AthloTrack.Services.WorkoutAnswerSettings? answerSettings = null,
        Guid? focusWorkoutId = null,
        IMeasurementFieldRepository? fields = null)
    {
        _athleteId = athleteId;
        _fields = fields;
        _answerSettings = answerSettings;
        _focusWorkoutId = focusWorkoutId;
        _athletes = athletes;
        _measurements = measurements;
        _workouts = workouts;
        _session = session;
        _avatars = avatars;
        _currentUser = currentUser;
        _ = LoadAsync();
    }

    private readonly IMeasurementFieldRepository? _fields;

    /// <summary>The coach's «Άλλες μετρήσεις» fields, for their names in the rows and the chart.</summary>
    private System.Collections.Generic.IReadOnlyList<MeasurementField> _fieldList = [];

    public ObservableCollection<Measurement> Measurements { get; } = new();
    public ObservableCollection<WorkoutProgram> Workouts { get; } = new();

    /// <summary>The lists as shown: by month, newest first, each month opened/closed by its header.</summary>
    [ObservableProperty]
    public partial System.Collections.Generic.IReadOnlyList<MeasurementMonth> MeasurementGroups { get; set; } = [];

    [ObservableProperty]
    public partial System.Collections.Generic.IReadOnlyList<WorkoutMonth> WorkoutGroups { get; set; } = [];

    public bool CanEdit => _session.IsCoach;

    /// <summary>Coach, or the athlete viewing their own profile: may change photo and basic details.</summary>
    public bool CanEditSelf => _session.IsCoach || _session.ProfileId == _athleteId;

    private bool IsOwnProfile => !_session.IsCoach && _session.ProfileId == _athleteId;

    /// <summary>Only the athlete changes their own photo — never the coach.</summary>
    public bool CanChangePhoto => IsOwnProfile;

    /// <summary>Only the athlete answers their own workouts (the coach is then notified).</summary>
    public bool CanCompleteWorkouts => IsOwnProfile;

    /// <summary>«✓ Θα το κάνει» for the coach, «✓ Θα το κάνεις» for the athlete.</summary>
    public string AcceptedLabel => CanEdit ? "✓ Θα το κάνει" : "✓ Θα το κάνεις";

    public string DeclinedLabel => CanEdit ? "✗ Δεν θα το κάνει" : "✗ Δεν θα το κάνεις";

    /// <summary>The coach can send reminders: the athlete has a login to receive them.</summary>
    public bool CanRemind => CanEdit && _athlete?.AuthUserId is not null;

    /// <summary>The workout a notification pointed at, once loaded (the view scrolls to it).</summary>
    [ObservableProperty]
    public partial WorkoutProgram? FocusedWorkout { get; set; }

    /// <summary>A short confirmation, e.g. «Στάλθηκε υπενθύμιση».</summary>
    [ObservableProperty]
    public partial string? InfoMessage { get; set; }

    [RelayCommand]
    private Task CompleteWorkoutAsync(WorkoutProgram? workout) =>
        AnswerAsync(workout, w => w.CanAnswerOutcome, "Ολοκληρώθηκε", id => _workouts.MarkCompletedAsync(id));

    [RelayCommand]
    private Task NotCompleteWorkoutAsync(WorkoutProgram? workout) =>
        AnswerAsync(workout, w => w.CanAnswerOutcome, "Δεν ολοκληρώθηκε", id => _workouts.MarkNotCompletedAsync(id));

    /// <summary>The athlete's «Θα το κάνω» on a new workout; the DB then notifies the coach.</summary>
    [RelayCommand]
    private Task AcceptWorkoutAsync(WorkoutProgram? workout) =>
        AnswerAsync(workout, w => w.IsPending, "Θα το κάνω", id => _workouts.AcceptAsync(id));

    /// <summary>
    /// The athlete's «Δεν θα το κάνω» (final). Always asked, whatever the setting: it carries the
    /// optional reason for the coach.
    /// </summary>
    [RelayCommand]
    private void DeclineWorkout(WorkoutProgram? workout)
    {
        if (workout is null || !workout.IsPending || !CanCompleteWorkouts) return;
        Ask(DeclineText, async () =>
            {
                await _workouts.DeclineAsync(workout.Id, ConfirmPrompt.CleanReason(ConfirmReason));
                await LoadAsync();
            },
            "Δεν θα το κάνω", danger: true, wantsReason: true);
    }

    internal const string DeclineText = "Δεν θα κάνεις αυτό το ασκησιολόγιο; Ο προπονητής θα ειδοποιηθεί. Αυτό δεν μπορεί να αναιρεθεί.";

    /// <summary>The coach cancels a workout (it stays, dimmed); the athlete is notified.</summary>
    [RelayCommand]
    private void CancelWorkout(WorkoutProgram? workout)
    {
        if (workout is null || !workout.CanBeCancelled || !CanEdit) return;
        Ask(CancelText(workout.TargetDate), () => CancelOrRestoreAsync(() => _workouts.CancelAsync(workout.Id)),
            "Ναι, ακύρωση");
    }

    internal static string CancelText(DateOnly date) =>
        $"Ακύρωση του ασκησιολογίου της {date:dd/MM}; Ο αθλητής θα ειδοποιηθεί.";

    /// <summary>The coach undoes a cancel; the athlete is notified.</summary>
    [RelayCommand]
    private Task RestoreWorkoutAsync(WorkoutProgram? workout)
    {
        if (workout is null || !workout.IsCancelled || !CanEdit) return Task.CompletedTask;
        return CancelOrRestoreAsync(() => _workouts.RestoreAsync(workout.Id));
    }

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
            ErrorMessage = CancelRefusalText(ex.Reason);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    internal static string CancelRefusalText(CancelRefusal reason) => reason switch
    {
        CancelRefusal.AlreadyAnswered => "Ο αθλητής έχει ήδη απαντήσει γι' αυτό το ασκησιολόγιο.",
        CancelRefusal.NotAllowed => "Μόνο ο προπονητής του αθλητή μπορεί να το ακυρώσει.",
        _ => "Η ακύρωση δεν είναι ακόμα διαθέσιμη (λείπει το 013_workout_accept_cancel.sql).",
    };

    /// <summary>The athlete's choice and answer are final: asked first, unless turned off in Ρυθμίσεις.</summary>
    private async Task AnswerAsync(WorkoutProgram? workout, Func<WorkoutProgram, bool> allowed, string button, Func<Guid, Task> send)
    {
        if (workout is null || !allowed(workout) || !CanCompleteWorkouts) return;
        async Task Run()
        {
            await send(workout.Id);
            await LoadAsync();
        }

        if (_answerSettings?.ConfirmAnswers ?? true)
        {
            Ask(AthloTrack.Services.WorkoutAnswerSettings.ConfirmText, Run, button, danger: false);
            return;
        }

        try
        {
            await Run();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task RemindWorkoutAsync(WorkoutProgram? workout)
    {
        if (workout is null || !workout.CanBeReminded || !CanRemind) return;
        ErrorMessage = null;
        InfoMessage = null;
        try
        {
            await _workouts.RemindAsync(workout.Id);
            InfoMessage = $"Στάλθηκε υπενθύμιση στον/στην {FullName} για το ασκησιολόγιο της {workout.TargetDate:dd/MM}.";
            await LoadAsync();
        }
        catch (WorkoutReminderException ex)
        {
            ErrorMessage = ReminderText(ex.Reason);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    internal static string ReminderText(ReminderRefusal reason) => reason switch
    {
        ReminderRefusal.TooSoon => "Έχει σταλεί υπενθύμιση την τελευταία ώρα. Δοκίμασε ξανά αργότερα.",
        ReminderRefusal.AlreadyAnswered => "Ο αθλητής έχει ήδη απαντήσει γι' αυτό το ασκησιολόγιο.",
        ReminderRefusal.NotAllowed => "Μόνο ο προπονητής του αθλητή μπορεί να στείλει υπενθύμιση.",
        _ => "Οι υπενθυμίσεις δεν είναι ακόμα διαθέσιμες (λείπει το 012_workout_outcome_reminders.sql).",
    };

    // Progress chart (weight + fat metrics over time). Plain data; the view draws it.
    [ObservableProperty]
    public partial ProgressChart? Chart { get; set; }

    public bool HasChartData => Measurements.Count >= 2;

    /// <summary>Exactly one measurement: explain why there is no chart yet.</summary>
    public bool ShowChartHint => !IsLoading && Measurements.Count == 1;

    public bool HasNoMeasurements => !IsLoading && Measurements.Count == 0;
    public bool HasNoWorkouts => !IsLoading && Workouts.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Initial))]
    public partial string FullName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Subtitle { get; set; }

    [ObservableProperty]
    public partial string? ProfileImagePath { get; set; }

    /// <summary>Profile photo bytes; null shows the initial instead.</summary>
    [ObservableProperty]
    public partial byte[]? Photo { get; set; }

    public string Initial => string.IsNullOrEmpty(FullName) ? "?" : FullName.Substring(0, 1).ToUpperInvariant();

    [ObservableProperty]
    public partial bool IsUploadingPhoto { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSkeleton), nameof(HasNoMeasurements), nameof(HasNoWorkouts), nameof(ShowChartHint))]
    public partial bool IsLoading { get; set; }

    /// <summary>
    /// The first load is running: show placeholders shaped like the content. Later reloads keep
    /// the content and only show the loading line.
    /// </summary>
    public bool ShowSkeleton => IsLoading && !_hasLoaded;

    private bool _hasLoaded;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    // In-app confirmation: the browser/Android heads have no native message box.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConfirming))]
    public partial string? ConfirmMessage { get; set; }

    public bool IsConfirming => ConfirmMessage is not null;

    [ObservableProperty]
    public partial string ConfirmButtonText { get; set; } = "Διαγραφή";

    /// <summary>Deletes use the red button; answers the blue one.</summary>
    [ObservableProperty]
    public partial bool ConfirmIsDanger { get; set; } = true;

    /// <summary>The dialog also asks for an optional reason («Δεν θα το κάνω»).</summary>
    [ObservableProperty]
    public partial bool ConfirmWantsReason { get; set; }

    [ObservableProperty]
    public partial string ConfirmReason { get; set; } = string.Empty;

    /// <summary>Raised by the "+" menu — the shell opens the add-measurement dialog.</summary>
    public event Action<Guid>? AddMeasurementRequested;

    /// <summary>Raised by the "+" menu — the shell opens the add-workout dialog.</summary>
    public event Action<Guid, string>? AddWorkoutRequested;

    /// <summary>The shell opens the measurement form pre-filled for editing.</summary>
    public event Action<Measurement>? EditMeasurementRequested;

    /// <summary>The shell opens the athlete form pre-filled for editing.</summary>
    public event Action<Athlete>? EditAthleteRequested;

    /// <summary>The shell opens the workout form pre-filled for editing (workout, athlete name).</summary>
    public event Action<WorkoutProgram, string>? EditWorkoutRequested;

    /// <summary>The athlete no longer exists; the shell returns to the list.</summary>
    public event Action? AthleteDeleted;

    [RelayCommand]
    private void AddMeasurement() => AddMeasurementRequested?.Invoke(_athleteId);

    [RelayCommand]
    private void AddWorkout() => AddWorkoutRequested?.Invoke(_athleteId, FullName);

    [RelayCommand]
    private void EditAthlete()
    {
        if (_athlete is not null) EditAthleteRequested?.Invoke(_athlete);
    }

    [RelayCommand]
    private void DeleteAthlete()
    {
        if (_athlete is null) return;
        var athlete = _athlete;
        Ask($"Διαγραφή του αθλητή {athlete.FullName}; Θα διαγραφούν και όλες οι μετρήσεις και τα ασκησιολόγιά του.",
            async () =>
            {
                await _athletes.DeleteAsync(athlete.Id);
                await _avatars.RemoveAsync(athlete.ProfileImagePath);
                AthleteDeleted?.Invoke();
            });
    }

    [RelayCommand]
    private void EditMeasurement(Measurement? measurement)
    {
        if (measurement is not null) EditMeasurementRequested?.Invoke(measurement);
    }

    [RelayCommand]
    private void DeleteMeasurement(Measurement? measurement)
    {
        if (measurement is null) return;
        Ask($"Διαγραφή της μέτρησης της {measurement.MeasuredAt:dd/MM/yyyy};",
            async () =>
            {
                await _measurements.DeleteAsync(measurement.Id);
                await LoadAsync();
            });
    }

    [RelayCommand]
    private void EditWorkout(WorkoutProgram? workout)
    {
        if (workout is not null && CanEdit) EditWorkoutRequested?.Invoke(workout, FullName);
    }

    [RelayCommand]
    private void DeleteWorkout(WorkoutProgram? workout)
    {
        if (workout is null || !CanEdit) return;
        Ask($"Διαγραφή του ασκησιολογίου της {workout.TargetDate:dd/MM/yyyy};",
            async () =>
            {
                await _workouts.DeleteAsync(workout.Id);
                await LoadAsync();
            });
    }

    /// <summary>The athlete has had this workout on screen: the coach gets the read receipt.</summary>
    // Concurrent: several workouts come into view at once; otherwise CanExecute is false while one is saving.
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task WorkoutSeenAsync(WorkoutProgram? workout)
    {
        if (workout is null || workout.IsRead || !IsOwnProfile) return;
        workout.ReadAt = DateTimeOffset.UtcNow; // don't send it twice
        await MarkWorkoutReadAsync(_workouts, workout.Id);
    }

    /// <summary>Records the read receipt; a failure must never break the screen.</summary>
    internal static async Task MarkWorkoutReadAsync(IWorkoutRepository repository, Guid workoutId)
    {
        try
        {
            await repository.MarkReadAsync(workoutId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AthloTrack] mark_workout_read failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        var action = _pendingConfirm;
        _pendingConfirm = null;
        ConfirmMessage = null;
        if (action is null) return;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void CancelConfirm()
    {
        _pendingConfirm = null;
        ConfirmMessage = null;
    }

    private void Ask(string message, Func<Task> onConfirm, string button = "Διαγραφή", bool danger = true, bool wantsReason = false)
    {
        _pendingConfirm = onConfirm;
        ConfirmButtonText = button;
        ConfirmIsDanger = danger;
        ConfirmReason = string.Empty;
        ConfirmWantsReason = wantsReason;
        ConfirmMessage = message;
    }

    /// <summary>Stores a new profile photo (already resized by the view) and replaces the old one.</summary>
    public async Task UploadPhotoAsync(byte[] image, string contentType)
    {
        if (_athlete is null || !CanChangePhoto) return;
        IsUploadingPhoto = true;
        ErrorMessage = null;
        try
        {
            var oldPath = _athlete.ProfileImagePath;
            var newPath = await _avatars.UploadAsync(_athlete.Id, image, contentType);
            _athlete.ProfileImagePath = newPath;
            _athlete = await _athletes.UpdateAsync(_athlete);
            ProfileImagePath = newPath;
            Photo = image;
            if (IsOwnProfile) _currentUser?.OnOwnAthleteChanged(_athlete, image);
            await _avatars.RemoveAsync(oldPath);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Η φωτογραφία δεν αποθηκεύτηκε: {ex.Message}";
        }
        finally
        {
            IsUploadingPhoto = false;
        }
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var athlete = await _athletes.GetByIdAsync(_athleteId);
            _athlete = athlete;
            if (athlete is not null)
            {
                FullName = athlete.FullName;
                ProfileImagePath = athlete.ProfileImagePath;
                var bits = new System.Collections.Generic.List<string>();
                if (athlete.HeightCm is { } h) bits.Add($"Ύψος: {h} cm");
                if (athlete.DateOfBirth is { } dob) bits.Add($"Γεν.: {dob:dd/MM/yyyy}");
                Subtitle = string.Join("  •  ", bits);
            }

            Measurements.Clear();
            var loaded = await _measurements.GetForAthleteAsync(_athleteId);
            if (_fields is not null && loaded.Any(m => m.Extra.Count > 0))
            {
                try
                {
                    _fieldList = await _fields.GetAllAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AthloTrack] measurement fields failed to load: {ex.Message}");
                }
            }
            foreach (var m in loaded)
            {
                m.ExtraLines = ExtraLinesFor(m, _fieldList);
                Measurements.Add(m);
            }

            BuildChart();

            Workouts.Clear();
            foreach (var w in await _workouts.GetForAthleteAsync(_athleteId))
                Workouts.Add(w);

            // A reload (after an edit) keeps the months the user opened or closed.
            MeasurementGroups = MonthGroups.Build(Measurements, MeasurementGroups);
            WorkoutGroups = MonthGroups.Build(Workouts, WorkoutGroups);
            OnPropertyChanged(nameof(CanRemind));

            // Opened from a notification: open that workout's month and point the view at it.
            if (_focusWorkoutId is { } focusId && Workouts.FirstOrDefault(w => w.Id == focusId) is { } focus)
            {
                _focusWorkoutId = null;
                foreach (var month in WorkoutGroups)
                {
                    if (!month.Items.Contains(focus)) continue;
                    month.IsExpanded = true;
                    foreach (var week in month.Weeks)
                    {
                        if (week.Items.Contains(focus)) week.IsExpanded = true;
                    }
                }
                FocusedWorkout = focus;
            }
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

        // Photo last, so the page is usable while it downloads.
        Photo = await _avatars.GetAsync(ProfileImagePath);
    }

    /// <summary>«Περιφέρεια μέσης: 82 cm» per custom value of a known field, in the fields' order.</summary>
    internal static System.Collections.Generic.List<string> ExtraLinesFor(Measurement m,
        System.Collections.Generic.IReadOnlyList<MeasurementField> fields) =>
        fields.Where(f => m.Extra.ContainsKey(f.Id))
            .Select(f => string.IsNullOrWhiteSpace(f.Unit)
                ? $"{f.Name}: {m.Extra[f.Id]}"
                : $"{f.Name}: {m.Extra[f.Id]} {f.Unit}")
            .ToList();

    /// <summary>Line colours for «Άλλες μετρήσεις», after the weight / fat ones.</summary>
    private static readonly string[] ExtraColors = ["#8E24AA", "#E53935", "#00897B", "#6D4C41", "#3949AB", "#F4511E", "#546E7A", "#C0CA33"];

    private void BuildChart()
    {
        var ordered = Measurements.OrderBy(m => m.MeasuredAt).ToList();
        if (ordered.Count < 2)
        {
            Chart = null;
            OnPropertyChanged(nameof(HasChartData));
            return;
        }

        // Brand blue for weight (with a soft fill), green and amber for the fat metrics.
        // Weight is optional (016): its line only when some measurement has one.
        var lines = new System.Collections.Generic.List<ChartLine>();
        if (ordered.Any(m => m.HasWeight))
            lines.Add(new("Βάρος (kg)", "#1565C0", ordered.Select(m => (double?)m.WeightKg).ToArray(), Width: 3, Fill: true));
        if (ordered.Any(m => m.FatMassWt is not null))
            lines.Add(new("Fat Mass/WT", "#009941", ordered.Select(m => (double?)m.FatMassWt).ToArray()));
        if (ordered.Any(m => m.FatHgt is not null))
            lines.Add(new("fat/hgt", "#FFB300", ordered.Select(m => (double?)m.FatHgt).ToArray()));
        var color = 0;
        foreach (var field in _fieldList.Where(f => ordered.Any(m => m.Extra.ContainsKey(f.Id))))
        {
            lines.Add(new(field.Label, ExtraColors[color++ % ExtraColors.Length],
                ordered.Select(m => m.Extra.TryGetValue(field.Id, out var v) ? (double?)v : null).ToArray()));
        }

        Chart = new ProgressChart(ordered.Select(m => m.MeasuredAt.ToString("dd/MM")).ToArray(), lines);
        OnPropertyChanged(nameof(HasChartData));
    }
}
