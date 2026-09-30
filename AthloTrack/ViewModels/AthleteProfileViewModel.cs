using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;
using AthloTrack.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace AthloTrack.ViewModels;

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

    // Progress chart (weight + fat metrics over time)
    [ObservableProperty]
    public partial ISeries[] ChartSeries { get; set; } = Array.Empty<ISeries>();

    [ObservableProperty]
    public partial Axis[] ChartXAxes { get; set; } = Array.Empty<Axis>();

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
    [NotifyPropertyChangedFor(nameof(HasNoMeasurements), nameof(HasNoWorkouts), nameof(ShowChartHint))]
    public partial bool IsLoading { get; set; }

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
            IsLoading = false;
        }

        // Photo last, so the page is usable while it downloads.
        Photo = await _avatars.GetAsync(ProfileImagePath);
    }

    private void BuildChart()
    {
        var ordered = Measurements.OrderBy(m => m.MeasuredAt).ToList();

        ChartXAxes = new[]
        {
            new Axis
            {
                Labels = ordered.Select(m => m.MeasuredAt.ToString("dd/MM")).ToArray(),
                LabelsRotation = 0,
                TextSize = 11,
            }
        };

        var series = new System.Collections.Generic.List<ISeries>
        {
            new LineSeries<double?>
            {
                Name = "Βάρος (kg)",
                Values = ordered.Select(m => (double?)m.WeightKg).ToArray(),
                Stroke = new SolidColorPaint(new SKColor(0x15, 0x65, 0xC0)) { StrokeThickness = 3 },
                GeometryStroke = new SolidColorPaint(new SKColor(0x15, 0x65, 0xC0)) { StrokeThickness = 3 },
                // Soft brand-blue fade under the weight line.
                Fill = new LinearGradientPaint(
                    new[] { new SKColor(0x15, 0x65, 0xC0, 0x55), new SKColor(0x15, 0x65, 0xC0, 0x00) },
                    new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)),
            },
        };

        if (ordered.Any(m => m.FatMassWt is not null))
        {
            series.Add(new LineSeries<double?>
            {
                Name = "Fat Mass/WT",
                Values = ordered.Select(m => (double?)m.FatMassWt).ToArray(),
                Stroke = new SolidColorPaint(new SKColor(0x00, 0x99, 0x41)) { StrokeThickness = 2 },
                GeometryStroke = new SolidColorPaint(new SKColor(0x00, 0x99, 0x41)) { StrokeThickness = 2 },
                Fill = null,
            });
        }

        if (ordered.Any(m => m.FatHgt is not null))
        {
            series.Add(new LineSeries<double?>
            {
                Name = "fat/hgt",
                Values = ordered.Select(m => (double?)m.FatHgt).ToArray(),
                Stroke = new SolidColorPaint(new SKColor(0xFF, 0xB3, 0x00)) { StrokeThickness = 2 },
                GeometryStroke = new SolidColorPaint(new SKColor(0xFF, 0xB3, 0x00)) { StrokeThickness = 2 },
                Fill = null,
            });
        }

        ChartSeries = series.ToArray();
        OnPropertyChanged(nameof(HasChartData));
    }
}
