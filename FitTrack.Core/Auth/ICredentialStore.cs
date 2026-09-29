namespace FitTrack.Core.Auth;

/// <summary>
/// Persists the Supabase session (access + refresh token) so the app can restore login
/// without asking for a password again, until the user manually logs out.
/// Implemented per-platform: Android uses Keystore-backed EncryptedSharedPreferences,
/// Browser uses IndexedDB, Desktop uses DPAPI-protected local storage.
/// </summary>
public interface ICredentialStore
{
    Task SaveSessionAsync(string accessToken, string refreshToken);
    Task<(string? AccessToken, string? RefreshToken)> LoadSessionAsync();
    Task ClearAsync();
}
