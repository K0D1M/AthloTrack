using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;
using AthloTrack.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

public partial class AddWorkoutViewModel : ObservableValidator
{
    private readonly Guid _athleteId;
    private readonly IWorkoutRepository _workouts;
    private readonly SessionState _session;

    public AddWorkoutViewModel(Guid athleteId, string athleteName, IWorkoutRepository workouts, SessionState session)
    {
        _athleteId = athleteId;
        _workouts = workouts;
        _session = session;
        Title = $"Νέο ασκησιολόγιο για τον αθλητή {athleteName}";
        if (!string.IsNullOrEmpty(athleteName)) Initial = athleteName.Substring(0, 1).ToUpperInvariant();
    }

    public event Action? Saved;
    public event Action? Cancelled;

    /// <summary>Set by <see cref="BeginEdit"/>: the workout being changed instead of a new one.</summary>
    private Guid? _editingId;

    /// <summary>Header shown at the top of the form.</summary>
    public string Title { get; private set; }

    /// <summary>Top-bar title of the page.</summary>
    public string PageTitle => _editingId is null ? "Νέο ασκησιολόγιο" : "Επεξεργασία ασκησιολογίου";

    /// <summary>Pre-fills the form with an existing workout (coach edit).</summary>
    public void BeginEdit(WorkoutProgram workout, string athleteName)
    {
        _editingId = workout.Id;
        Content = workout.Content;
        CoachPresent = workout.CoachPresent;
        TargetDate = new DateTimeOffset(workout.TargetDate.ToDateTime(TimeOnly.MinValue));
        Title = $"Επεξεργασία ασκησιολογίου για τον αθλητή {athleteName}";
    }

    /// <summary>The athlete's photo for the header; null shows <see cref="Initial"/>.</summary>
    [ObservableProperty]
    public partial byte[]? Photo { get; set; }

    public string Initial { get; private set; } = "?";

    [ObservableProperty]
    public partial DateTimeOffset TargetDate { get; set; } = DateTimeOffset.Now;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Το ασκησιολόγιο δεν μπορεί να είναι κενό.")]
    [MinLength(3, ErrorMessage = "Πολύ σύντομο κείμενο.")]
    public partial string Content { get; set; } = string.Empty;

    /// <summary>Optional: true Παρών, false Απών, null not said (no indicator for the athlete).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPresentChosen), nameof(IsAbsentChosen))]
    public partial bool? CoachPresent { get; set; }

    /// <summary>The «Παρών» toggle. Tapping it again when chosen clears the choice.</summary>
    public bool IsPresentChosen
    {
        get => CoachPresent == true;
        set => CoachPresent = value ? true : CoachPresent == true ? null : CoachPresent;
    }

    /// <summary>The «Απών» toggle. Tapping it again when chosen clears the choice.</summary>
    public bool IsAbsentChosen
    {
        get => CoachPresent == false;
        set => CoachPresent = value ? false : CoachPresent == false ? null : CoachPresent;
    }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        ValidateAllProperties();
        if (HasErrors)
        {
            ErrorMessage = "Παρακαλώ συμπληρώστε το ασκησιολόγιο.";
            return;
        }

        IsBusy = true;
        try
        {
            if (_editingId is { } id)
            {
                // The DB clears the read receipt and sends the athlete "ενημέρωσε το ασκησιολόγιο".
                await _workouts.UpdateAsync(id, Content.Trim(), DateOnly.FromDateTime(TargetDate.DateTime), CoachPresent);
                Saved?.Invoke();
                return;
            }

            var program = new WorkoutProgram
            {
                AthleteId = _athleteId,
                Content = Content.Trim(),
                TargetDate = DateOnly.FromDateTime(TargetDate.DateTime),
                CreatedBy = _session.ProfileId,
                CoachPresent = CoachPresent,
            };

            // The DB trigger auto-creates the athlete's "νέο ασκησιολόγιο" notification.
            await _workouts.AddAsync(program);
            Saved?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
