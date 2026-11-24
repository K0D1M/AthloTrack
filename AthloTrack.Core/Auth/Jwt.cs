using System.Text;
using System.Text.Json;

namespace AthloTrack.Core.Auth;

/// <summary>Reads a Supabase access token's claims locally (no signature check: only for our own decisions).</summary>
public static class Jwt
{
    /// <summary>The token's expiry («exp»), or null when it can't be read.</summary>
    public static DateTimeOffset? ExpiresAt(string? token) =>
        Claim(token, "exp") is { ValueKind: JsonValueKind.Number } exp && exp.TryGetInt64(out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;

    /// <summary>The login («sub»), or null when it can't be read.</summary>
    public static Guid? Subject(string? token) =>
        Claim(token, "sub") is { ValueKind: JsonValueKind.String } sub && Guid.TryParse(sub.GetString(), out var id) ? id : null;

    private static JsonElement? Claim(string? token, string name)
    {
        var parts = token?.Split('.');
        if (parts is not { Length: 3 }) return null;
        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var json = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            return json.RootElement.TryGetProperty(name, out var value) ? value.Clone() : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
