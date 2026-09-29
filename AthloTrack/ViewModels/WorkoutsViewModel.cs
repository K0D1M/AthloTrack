using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AthloTrack.Core.Data;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AthloTrack.ViewModels;

/// <summary>Προπονήσεις — list of all current workout programs across athletes.</summary>
public partial class WorkoutsViewModel : ViewModelBase
{
    private readonly IWorkoutRepository _workouts;
    private readonly IAthleteRepository _athletes;

    public WorkoutsViewModel(IWorkoutRepository workouts, IAthleteRepository athletes)
    {
        _workouts = workouts;
        _athletes = athletes;
        _ = LoadAsync();
    }

    public ObservableCollection<WorkoutListItemViewModel> WorkoutPrograms { get; } = new();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var athletes = await _athletes.GetAllAsync();
            var names = new Dictionary<Guid, string>();
            foreach (var a in athletes) names[a.Id] = a.FullName;

            var programs = await _workouts.GetAllAsync();
            WorkoutPrograms.Clear();
            foreach (var p in programs)
            {
                var name = names.TryGetValue(p.AthleteId, out var n) ? n : "—";
                WorkoutPrograms.Add(new WorkoutListItemViewModel(p, name));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
