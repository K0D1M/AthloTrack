using Supabase;

namespace AthloTrack.Core.Supabase;

public sealed class SupabaseClientFactory
{
    private readonly SupabaseConfig _config;
    private Client? _client;

    public SupabaseClientFactory(SupabaseConfig config)
    {
        _config = config;
    }

    public async Task<Client> GetClientAsync()
    {
        if (_client is not null)
        {
            return _client;
        }

        var options = new SupabaseOptions
        {
            AutoRefreshToken = true,
            // Realtime is intentionally unused (in-app polling only) — no WebSocket connection needed.
            AutoConnectRealtime = false,
        };

        var client = new Client(_config.Url, _config.AnonKey, options);
        await client.InitializeAsync();
        _client = client;
        return client;
    }
}
