using AthloTrack.Core.Supabase;

namespace AthloTrack.Core.Push;

/// <summary>Supplies this device's push token (Firebase on Android). Heads without push register none.</summary>
public interface IPushTokenProvider
{
    string Platform { get; }
    Task<string?> GetTokenAsync();
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

    /// <summary>After sign-in / session restore.</summary>
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
