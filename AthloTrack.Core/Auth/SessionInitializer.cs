using AthloTrack.Core.Data;

namespace AthloTrack.Core.Auth;

public sealed class SessionInitializer : ISessionInitializer
{
    private readonly SessionState _session;
    private readonly ICoachRepository _coaches;
    private readonly IAthleteRepository _athletes;

    public SessionInitializer(SessionState session, ICoachRepository coaches, IAthleteRepository athletes)
    {
        _session = session;
        _coaches = coaches;
        _athletes = athletes;
    }

    public async Task<bool> InitializeAsync(UserRole role, Guid authUserId)
    {
        if (role == UserRole.Coach)
        {
            var coach = await _coaches.GetByAuthUserIdAsync(authUserId);
            if (coach is null)
            {
                return false;
            }

            _session.AuthUserId = authUserId;
            _session.Role = UserRole.Coach;
            _session.ProfileId = coach.Id;
            _session.DisplayName = coach.FullName;
            _session.ProfileImagePath = coach.ProfileImagePath;
            _session.CoachId = null;
            return true;
        }

        var athlete = await _athletes.GetByAuthUserIdAsync(authUserId);
        if (athlete is null)
        {
            return false;
        }

        _session.AuthUserId = authUserId;
        _session.Role = UserRole.Athlete;
        _session.ProfileId = athlete.Id;
        _session.DisplayName = athlete.FullName;
        _session.ProfileImagePath = athlete.ProfileImagePath;
        _session.CoachId = athlete.CoachId;
        return true;
    }
}
