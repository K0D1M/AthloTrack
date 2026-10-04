using System;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;

namespace AthloTrack.ViewModels;

public sealed class AddWorkoutViewModelFactory
{
    private readonly IWorkoutRepository _workouts;
    private readonly SessionState _session;
    private readonly IAthleteRepository _athletes;
    private readonly IAvatarService _avatars;
    private readonly IAppPreferences _preferences;
    private readonly IWorkoutTemplateRepository _templates;

    public AddWorkoutViewModelFactory(
        IWorkoutRepository workouts,
        SessionState session,
        IAthleteRepository athletes,
        IAvatarService avatars,
        IAppPreferences preferences,
        IWorkoutTemplateRepository templates)
    {
        _workouts = workouts;
        _session = session;
        _athletes = athletes;
        _avatars = avatars;
        _preferences = preferences;
        _templates = templates;
    }

    public AddWorkoutViewModel Create(Guid athleteId, string athleteName)
    {
        var vm = New(athleteId, athleteName);
        _ = LoadPhotoAsync(vm, athleteId);
        return vm;
    }

    public AddWorkoutViewModel CreateForEdit(AthloTrack.Core.Models.WorkoutProgram workout, string athleteName)
    {
        var vm = New(workout.AthleteId, athleteName);
        vm.BeginEdit(workout, athleteName);
        _ = LoadPhotoAsync(vm, workout.AthleteId);
        return vm;
    }

    private AddWorkoutViewModel New(Guid athleteId, string athleteName) =>
        new(athleteId, athleteName, _workouts, _session, _preferences, _templates, _athletes);

    private async Task LoadPhotoAsync(AddWorkoutViewModel vm, Guid athleteId)
    {
        try
        {
            var athlete = await _athletes.GetByIdAsync(athleteId);
            vm.Photo = await _avatars.GetAsync(athlete?.ProfileImagePath);
        }
        catch
        {
            // The header just keeps the initial.
        }
    }
}
