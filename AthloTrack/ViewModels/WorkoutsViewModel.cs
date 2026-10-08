using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

/// <summary>Προπονήσεις — list of all current workout programs across athletes.</summary>
public partial class WorkoutsViewModel : ViewModelBase
{
    private readonly IWorkoutRepository _workouts;
    private readonly IAthleteRepository _athletes;
    private readonly SessionState _session;
    private readonly AthloTrack.Services.WorkoutAnswerSettings? _answerSettings;

    public WorkoutsViewModel(IWorkoutRepository workouts, IAthleteRepository athletes, SessionState session,
        AthloTrack.Services.WorkoutAnswerSettings? answerSettings = null)
    {
        _workouts = workouts;
        _athletes = athletes;
        _session = session;
        _answerSettings = answerSettings;
        _ = LoadAsync();
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
        AnswerAsync(item, "Ολοκληρώθηκε", id => _workouts.MarkCompletedAsync(id));

    /// <summary>The athlete answers «Δεν ολοκληρώθηκε»; the DB then notifies the coach.</summary>
    [RelayCommand]
    private Task NotCompleteWorkoutAsync(WorkoutListItemViewModel? item) =>
        AnswerAsync(item, "Δεν ολοκληρώθηκε", id => _workouts.MarkNotCompletedAsync(id));

    private async Task AnswerAsync(WorkoutListItemViewModel? item, string button, Func<Guid, Task> send)
    {
        if (item is null || !item.CanComplete) return;
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

            var programs = await _workouts.GetAllAsync();
            WorkoutPrograms.Clear();
            foreach (var p in programs)
            {
                var name = names.TryGetValue(p.AthleteId, out var n) ? n : "—";
                WorkoutPrograms.Add(new WorkoutListItemViewModel(p, name, isCoach: _session.IsCoach,
                    athleteHasLogin: withLogin.Contains(p.AthleteId)));
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
    }
}
