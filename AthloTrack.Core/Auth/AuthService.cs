using AthloTrack.Core.Supabase;

namespace AthloTrack.Core.Auth;

public sealed class AuthService : IAuthService
{
    private readonly SupabaseClientFactory _clientFactory;
    private readonly ICredentialStore _credentialStore;
    private readonly AthloTrack.Core.Offline.OfflineCache? _offline;

    public AuthService(SupabaseClientFactory clientFactory, ICredentialStore credentialStore,
        AthloTrack.Core.Offline.OfflineCache? offline = null)
    {
        _clientFactory = clientFactory;
        _credentialStore = credentialStore;
        _offline = offline;
    }

    public async Task<AuthResult> SignInAsync(string email, string password)
    {
        var client = await _clientFactory.GetClientAsync();
        try
        {
            var session = await client.Auth.SignInWithPassword(email, password);
            if (session?.AccessToken is null || session.RefreshToken is null || session.User?.Id is null)
            {
                return AuthResult.Fail("Λανθασμένα στοιχεία σύνδεσης.");
            }

            await _credentialStore.SaveSessionAsync(session.AccessToken, session.RefreshToken);
            return AuthResult.Ok(Guid.Parse(session.User.Id), session.AccessToken, session.RefreshToken);
        }
        catch (Exception ex)
        {
            return AuthResult.Fail(AuthErrors.Describe(ex));
        }
    }

    public async Task<AuthResult> SignUpAsync(string email, string password)
    {
        var client = await _clientFactory.GetClientAsync();
        try
        {
            var session = await client.Auth.SignUp(email, password);
            if (session?.AccessToken is not null && session.RefreshToken is not null && session.User?.Id is not null)
            {
                await _credentialStore.SaveSessionAsync(session.AccessToken, session.RefreshToken);
                return AuthResult.Ok(Guid.Parse(session.User.Id), session.AccessToken, session.RefreshToken);
            }

            // Account created, but the project requires the email link to be confirmed first.
            return new AuthResult(true, null, null, null, null);
        }
        catch (Exception ex)
        {
            return AuthResult.Fail(AuthErrors.Describe(ex));
        }
    }

    public async Task<AuthResult> RestoreSessionAsync()
    {
        var (accessToken, refreshToken) = await _credentialStore.LoadSessionAsync();
        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
        {
            return AuthResult.Fail("Δεν υπάρχει αποθηκευμένη σύνδεση.");
        }

        try
        {
            // The client factory takes up the saved session itself (renewed if needed); setting it
            // again here would reuse a refresh token that was just rotated.
            var client = await _clientFactory.GetClientAsync();
            var session = client.Auth.CurrentSession?.User?.Id is not null
                ? client.Auth.CurrentSession
                : await client.Auth.SetSession(accessToken, refreshToken, false);
            if (session?.AccessToken is null || session.RefreshToken is null || session.User?.Id is null)
            {
                return AuthResult.Fail("Η σύνδεση έληξε, παρακαλώ συνδεθείτε ξανά.");
            }

            await _credentialStore.SaveSessionAsync(session.AccessToken, session.RefreshToken);
            return AuthResult.Ok(Guid.Parse(session.User.Id), session.AccessToken, session.RefreshToken);
        }
        catch (Exception ex) when (AthloTrack.Core.Offline.OfflineState.IsNetworkError(ex)
                                   && Jwt.Subject(accessToken) is { } offlineUser)
        {
            // No internet: keep the saved sign-in (it was signing people out); the app may open
            // from its offline copy and check the session once the internet is back.
            return AuthResult.Offline(offlineUser);
        }
        catch (Exception ex)
        {
            await _credentialStore.ClearAsync();
            return AuthResult.Fail(AuthErrors.Describe(ex));
        }
    }

    public async Task<Guid?> StoredUserIdAsync()
    {
        var (accessToken, _) = await _credentialStore.LoadSessionAsync();
        return Jwt.Subject(accessToken);
    }

    public async Task<AuthResult> UpdatePasswordAsync(string newPassword)
    {
        var client = await _clientFactory.GetClientAsync();
        try
        {
            var user = await client.Auth.Update(new global::Supabase.Gotrue.UserAttributes { Password = newPassword });
            return user?.Id is null
                ? AuthResult.Fail("Ο κωδικός δεν άλλαξε.")
                : new AuthResult(true, null, Guid.Parse(user.Id), null, null);
        }
        catch (Exception ex)
        {
            return AuthResult.Fail(AuthErrors.Describe(ex));
        }
    }

    public async Task SignOutAsync()
    {
        var client = await _clientFactory.GetClientAsync();
        try
        {
            await client.Auth.SignOut(global::Supabase.Gotrue.Constants.SignOutScope.Local);
        }
        finally
        {
            await _credentialStore.ClearAsync();
            _offline?.ClearAll(); // nobody else on this phone sees the copies
        }
    }
}
