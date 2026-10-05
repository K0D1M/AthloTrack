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

    public AthleteProfileViewModel(
        Guid athleteId,
        IAthleteRepository athletes,
        IMeasurementRepository measurements,
        IWorkoutRepository workouts,
        SessionState session,
        IAvatarService avatars,
        CurrentUserViewModel? currentUser = null)
    {
        _athleteId = athleteId;
        _athletes = athletes;
        _measurements = measurements;
        _workouts = workouts;
        _session = session;
        _avatars = avatars;
        _currentUser = currentUser;
        _ = LoadAsync();
    }

    public ObservableCollection<Measurement> Measurements { get; } = new();
    public ObservableCollection<WorkoutProgram> Workouts { get; } = new();

    public bool CanEdit => _session.IsCoach;

    /// <summary>Coach, or the athlete viewing their own profile: may change photo and basic details.</summary>
    public bool CanEditSelf => _session.IsCoach || _session.ProfileId == _athleteId;

    private bool IsOwnProfile => !_session.IsCoach && _session.ProfileId == _athleteId;

    /// <summary>Only the athlete changes their own photo — never the coach.</summary>
    public bool CanChangePhoto => IsOwnProfile;

    /// <summary>Only the athlete marks their own workouts done (the coach is then notified).</summary>
    public bool CanCompleteWorkouts => IsOwnProfile;

    [RelayCommand]
    private async Task CompleteWorkoutAsync(WorkoutProgram? workout)
    {
        if (workout is null || workout.IsCompleted || !CanCompleteWorkouts) return;
        try
        {
            await _workouts.MarkCompletedAsync(workout.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

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

    private void Ask(string message, Func<Task> onConfirm)
    {
        _pendingConfirm = onConfirm;
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
            foreach (var m in await _measurements.GetForAthleteAsync(_athleteId))
                Measurements.Add(m);

            BuildChart();

            Workouts.Clear();
            foreach (var w in await _workouts.GetForAthleteAsync(_athleteId))
                Workouts.Add(w);
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
        var lines = new System.Collections.Generic.List<ChartLine>
        {
            new("Βάρος (kg)", "#1565C0", ordered.Select(m => (double?)m.WeightKg).ToArray(), Width: 3, Fill: true),
        };
        if (ordered.Any(m => m.FatMassWt is not null))
            lines.Add(new("Fat Mass/WT", "#009941", ordered.Select(m => (double?)m.FatMassWt).ToArray()));
        if (ordered.Any(m => m.FatHgt is not null))
            lines.Add(new("fat/hgt", "#FFB300", ordered.Select(m => (double?)m.FatHgt).ToArray()));

        Chart = new ProgressChart(ordered.Select(m => m.MeasuredAt.ToString("dd/MM")).ToArray(), lines);
        OnPropertyChanged(nameof(HasChartData));
    }
}
