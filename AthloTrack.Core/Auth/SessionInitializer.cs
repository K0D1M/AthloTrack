using AthloTrack.Core.Data;

namespace AthloTrack.Core.Auth;

public sealed class SessionInitializer : ISessionInitializer
{
    private readonly SessionState _session;
    private readonly ICoachRepository _coaches;
    private readonly IAthleteRepository _athletes;
    private readonly IAdminRepository _admins;
    private readonly AthloTrack.Core.Offline.OfflineCache? _offline;
    private readonly AthloTrack.Core.Offline.OfflineState? _offlineState;

    public SessionInitializer(SessionState session, ICoachRepository coaches, IAthleteRepository athletes,
        IAdminRepository admins, AthloTrack.Core.Offline.OfflineCache? offline = null,
        AthloTrack.Core.Offline.OfflineState? offlineState = null)
    {
        _session = session;
        _coaches = coaches;
        _athletes = athletes;
        _admins = admins;
        _offline = offline;
        _offlineState = offlineState;
    }

    public async Task<bool> InitializeAsync(UserRole role, Guid authUserId)
    {
        var found = await LookUpAsync(role, authUserId);
        // Coaches and athletes may open the app without internet next time (not admins).
        if (found && role != UserRole.Admin && _session.ProfileId is { } profile)
        {
            _offline?.SaveSession(authUserId, new AthloTrack.Core.Offline.SessionSnapshot(
                role, profile, _session.DisplayName, _session.ProfileImagePath, _session.CoachId));
        }
        return found;
    }

    public UserRole? RestoreOffline(Guid authUserId)
    {
        if (_offline?.LoadSession(authUserId) is not { } s) return null;
        _session.AuthUserId = authUserId;
        _session.Role = s.Role;
        _session.ProfileId = s.ProfileId;
        _session.DisplayName = s.DisplayName;
        _session.ProfileImagePath = s.ProfileImagePath;
        _session.CoachId = s.CoachId;
        _session.MustSetPassword = false;
        _offlineState?.SetOffline(null);
        return s.Role;
    }

    private async Task<bool> LookUpAsync(UserRole role, Guid authUserId)
    {
        if (role == UserRole.Admin)
        {
            var admin = await _admins.GetByAuthUserIdAsync(authUserId);
            if (admin is null)
            {
                return false;
            }

            _session.AuthUserId = authUserId;
            _session.Role = UserRole.Admin;
            _session.ProfileId = admin.Id;
            _session.DisplayName = admin.FullName;
            _session.ProfileImagePath = null;
            _session.CoachId = null;
            _session.MustSetPassword = false;
            return true;
        }

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
            _session.MustSetPassword = coach.MustSetPassword;
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
        _session.MustSetPassword = false;
        return true;
    }
}
