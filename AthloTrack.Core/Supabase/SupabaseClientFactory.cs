using System.Text;
using System.Text.Json;
using AthloTrack.Core.Auth;
using Supabase;

namespace AthloTrack.Core.Supabase;

public sealed class SupabaseClientFactory
{
    /// <summary>Renew the session when its token has less than this left (or has expired).</summary>
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(2);

    private readonly SupabaseConfig _config;
    private readonly ICredentialStore? _credentialStore;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private Client? _client;

    public SupabaseClientFactory(SupabaseConfig config, ICredentialStore? credentialStore = null)
    {
        _config = config;
        _credentialStore = credentialStore;
    }

    /// <summary>
    /// The client, with a session token that is still valid. Every repository gets the client
    /// here before each request, so this is where the token is renewed: the library's own timer
    /// doesn't run while a phone sleeps or a browser tab is in the background, and the app's first
    /// request after an hour away used to fail with «JWT expired».
    /// </summary>
    public async Task<Client> GetClientAsync()
    {
        var client = _client ?? await CreateAsync();
        await EnsureFreshSessionAsync(client);
        return client;
    }

    private async Task<Client> CreateAsync()
    {
        var options = new SupabaseOptions
        {
            AutoRefreshToken = true,
            // Realtime is intentionally unused (in-app polling only) — no WebSocket connection needed.
            AutoConnectRealtime = false,
        };

        var client = new Client(_config.Url, _config.AnonKey, options);
        await client.InitializeAsync();

        // Whenever the library renews the session (its timer, or a refresh below), keep the new
        // tokens: the stored refresh token is used once, so an old one signs the user out at the
        // next start.
        client.Auth.AddStateChangedListener((sender, state) =>
        {
            if (state != global::Supabase.Gotrue.Constants.AuthState.TokenRefreshed) return;
            var session = sender.CurrentSession;
            if (_credentialStore is not null && session?.AccessToken is { } access && session.RefreshToken is { } refresh)
                _ = _credentialStore.SaveSessionAsync(access, refresh);
        });

        _client = client;
        return client;
    }

    private async Task EnsureFreshSessionAsync(Client client)
    {
        if (client.Auth.CurrentSession is null && _credentialStore is not null)
        {
            await ResumeSavedSessionAsync(client);
            return;
        }
        if (!NeedsRefresh(client.Auth.CurrentSession?.AccessToken, client.Auth.CurrentSession?.RefreshToken)) return;

        await _refreshLock.WaitAsync();
        try
        {
            // Another request may have renewed it while this one waited.
            var session = client.Auth.CurrentSession;
            if (!NeedsRefresh(session?.AccessToken, session?.RefreshToken)) return;

            await client.Auth.RefreshSession();
            session = client.Auth.CurrentSession;
            if (_credentialStore is not null && session?.AccessToken is { } access && session.RefreshToken is { } refresh)
                await _credentialStore.SaveSessionAsync(access, refresh);
        }
        catch (Exception ex)
        {
            // Offline, or the refresh token is no longer valid: the request goes ahead and reports
            // its own error; the next sign-in starts a fresh session.
            Console.WriteLine($"[AthloTrack] session refresh failed: {ex.Message}");
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// The app opened without internet, from its offline copy: the client has no session yet.
    /// Once a request can reach the server, the saved session is taken up again (renewed if needed).
    /// </summary>
    private async Task ResumeSavedSessionAsync(Client client)
    {
        await _refreshLock.WaitAsync();
        try
        {
            if (client.Auth.CurrentSession is not null) return;
            var (access, refresh) = await _credentialStore!.LoadSessionAsync();
            if (string.IsNullOrEmpty(access) || string.IsNullOrEmpty(refresh)) return;
            var session = await client.Auth.SetSession(access, refresh, false);
            if (session?.AccessToken is { } a && session.RefreshToken is { } r)
                await _credentialStore.SaveSessionAsync(a, r);
        }
        catch (Exception ex)
        {
            // Still offline: the request reports it, and the offline copy is shown.
            Console.WriteLine($"[AthloTrack] saved session not resumed: {ex.Message}");
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static bool NeedsRefresh(string? accessToken, string? refreshToken) =>
        refreshToken is not null && ExpiresAt(accessToken) is { } expires
        && expires - DateTimeOffset.UtcNow < RefreshMargin;

    /// <summary>The access token's expiry («exp» in the JWT), or null when it can't be read.</summary>
    public static DateTimeOffset? ExpiresAt(string? accessToken) => Jwt.ExpiresAt(accessToken);
}
