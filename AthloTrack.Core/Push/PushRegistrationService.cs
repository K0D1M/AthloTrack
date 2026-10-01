using AthloTrack.Core.Supabase;

namespace AthloTrack.Core.Push;

/// <summary>Supplies this device's push token (Firebase on Android). Heads without push register none.</summary>
public interface IPushTokenProvider
{
    string Platform { get; }
    Task<string?> GetTokenAsync();
}

/// <summary>Where a provider that needs the user's go-ahead (browsers) stands.</summary>
public enum PushPermission
{
    /// <summary>This browser can't receive push.</summary>
    Unsupported,
    /// <summary>iPhone/iPad: only the web app added to the Home Screen can receive push.</summary>
    InstallFirst,
    /// <summary>Not asked yet: the user must tap to allow (browsers only ask after a tap).</summary>
    NotAsked,
    Granted,
    /// <summary>Blocked in the browser's site settings.</summary>
    Denied,
}

/// <summary>Implemented by push providers that need an explicit, user-initiated permission (web).</summary>
public interface IPushPermission
{
    PushPermission State { get; }

    /// <summary>Shows the browser's permission prompt; true when granted.</summary>
    Task<bool> RequestAsync();
}

/// <summary>
/// Links this device's push token to the signed-in user (table device_tokens), so the
/// "push" Edge Function can reach them. Safe to call on heads without push: it does nothing.
/// </summary>
public sealed class PushRegistrationService
{
    private readonly SupabaseClientFactory _factory;
    private readonly IPushTokenProvider? _provider;
    private string? _registeredToken;

    public PushRegistrationService(SupabaseClientFactory factory, IEnumerable<IPushTokenProvider> providers)
    {
        _factory = factory;
        _provider = providers.FirstOrDefault();
    }

    /// <summary>Null when this head has no permission step (Android asks at start-up; desktop has no push).</summary>
    public IPushPermission? Permission => _provider as IPushPermission;

    /// <summary>The user tapped "turn on notifications": ask, then link this device.</summary>
    public async Task<bool> EnableAsync()
    {
        if (Permission is not { } permission) return false;
        if (permission.State != PushPermission.Granted && !await permission.RequestAsync()) return false;
        await RegisterAsync();
        return _registeredToken is not null;
    }

    /// <summary>
    /// After sign-in / session restore. Providers never prompt here (browsers only allow the
    /// prompt after a tap), they just return the token when permission was already given.
    /// </summary>
    public async Task RegisterAsync()
    {
        if (_provider is null) return;
        try
        {
            var token = await _provider.GetTokenAsync();
            if (!string.IsNullOrEmpty(token)) await RegisterTokenAsync(token);
        }
        catch
        {
            // Push is best-effort; the in-app notification list still works.
        }
    }

    /// <summary>When the platform rotates the token while signed in.</summary>
    public async Task RegisterTokenAsync(string token)
    {
        var client = await _factory.GetClientAsync();
        if (client.Auth.CurrentSession is null) return;
        await client.Rpc("claim_device_token", new Dictionary<string, object>
        {
            ["p_token"] = token,
            ["p_platform"] = _provider?.Platform ?? "android",
        });
        _registeredToken = token;
    }

    /// <summary>Before sign-out: this phone should stop receiving the old user's notifications.</summary>
    public async Task UnregisterAsync()
    {
        if (_provider is null) return;
        try
        {
            var token = _registeredToken ?? await _provider.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return;
            var client = await _factory.GetClientAsync();
            await client.From<DeviceTokenRow>().Where(x => x.Token == token).Delete();
            _registeredToken = null;
        }
        catch
        {
            // ignore: the token moves to the next user who signs in on this phone anyway
        }
    }
}

[global::Supabase.Postgrest.Attributes.Table("device_tokens")]
public sealed class DeviceTokenRow : global::Supabase.Postgrest.Models.BaseModel
{
    [global::Supabase.Postgrest.Attributes.PrimaryKey("token", true)]
    public string Token { get; set; } = string.Empty;
}
