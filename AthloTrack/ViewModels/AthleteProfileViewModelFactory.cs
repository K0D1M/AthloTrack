using System;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;

namespace AthloTrack.ViewModels;

/// <summary>Creates an <see cref="AthleteProfileViewModel"/> for a specific athlete id at runtime.</summary>
public sealed class AthleteProfileViewModelFactory
{
    private readonly IAthleteRepository _athletes;
    private readonly IMeasurementRepository _measurements;
    private readonly IWorkoutRepository _workouts;
    private readonly SessionState _session;
    private readonly IAvatarService _avatars;
    private readonly CurrentUserViewModel _currentUser;
    private readonly AthloTrack.Services.WorkoutAnswerSettings _answerSettings;

    public AthleteProfileViewModelFactory(
        IAthleteRepository athletes,
        IMeasurementRepository measurements,
        IWorkoutRepository workouts,
        SessionState session,
        IAvatarService avatars,
        CurrentUserViewModel currentUser,
        AthloTrack.Services.WorkoutAnswerSettings answerSettings)
    {
        _answerSettings = answerSettings;
        _athletes = athletes;
        _measurements = measurements;
        _workouts = workouts;
        _session = session;
        _avatars = avatars;
        _currentUser = currentUser;
    }

    /// <param name="focusWorkoutId">Opened from a notification: show this workout.</param>
    public AthleteProfileViewModel Create(Guid athleteId, Guid? focusWorkoutId = null) =>
        new(athleteId, _athletes, _measurements, _workouts, _session, _avatars, _currentUser, _answerSettings, focusWorkoutId);
}
