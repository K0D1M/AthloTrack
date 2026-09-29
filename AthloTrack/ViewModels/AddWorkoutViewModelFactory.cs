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

    public AddWorkoutViewModelFactory(
        IWorkoutRepository workouts,
        SessionState session,
        IAthleteRepository athletes,
        IAvatarService avatars)
    {
        _workouts = workouts;
        _session = session;
        _athletes = athletes;
        _avatars = avatars;
    }

    public AddWorkoutViewModel Create(Guid athleteId, string athleteName)
    {
        var vm = new AddWorkoutViewModel(athleteId, athleteName, _workouts, _session);
        _ = LoadPhotoAsync(vm, athleteId);
        return vm;
    }

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
