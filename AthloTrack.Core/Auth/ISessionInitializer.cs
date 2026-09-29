namespace AthloTrack.Core.Auth;

public interface ISessionInitializer
{
    /// <summary>
    /// After authentication, looks up the coach/athlete profile row matching the signed-in
    /// auth user and populates <see cref="SessionState"/>. Returns false if no matching
    /// profile row exists for the chosen role (e.g. logged in as coach but no coaches row).
    /// </summary>
    Task<bool> InitializeAsync(UserRole role, Guid authUserId);
}
