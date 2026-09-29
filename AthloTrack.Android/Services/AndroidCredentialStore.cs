using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;

namespace AthloTrack.Android.Services;

/// <summary>
/// Android session storage in the app's private files directory, which is sandboxed per-app
/// by the OS. Simple and adequate for a tiny internal app; can be upgraded to
/// Keystore-backed EncryptedSharedPreferences later for defense-in-depth.
/// </summary>
public sealed class AndroidCredentialStore : ICredentialStore
{
    private static string SessionFile =>
        Path.Combine(
            global::Android.App.Application.Context.FilesDir!.AbsolutePath,
            "session.json");

    private sealed record StoredSession(string AccessToken, string RefreshToken);

    public Task SaveSessionAsync(string accessToken, string refreshToken)
    {
        var json = JsonSerializer.Serialize(new StoredSession(accessToken, refreshToken));
        File.WriteAllText(SessionFile, json);
        return Task.CompletedTask;
    }

    public Task<(string? AccessToken, string? RefreshToken)> LoadSessionAsync()
    {
        if (!File.Exists(SessionFile))
            return Task.FromResult<(string?, string?)>((null, null));

        try
        {
            var session = JsonSerializer.Deserialize<StoredSession>(File.ReadAllText(SessionFile));
            return Task.FromResult<(string?, string?)>((session?.AccessToken, session?.RefreshToken));
        }
        catch
        {
            return Task.FromResult<(string?, string?)>((null, null));
        }
    }

    public Task ClearAsync()
    {
        try
        {
            if (File.Exists(SessionFile))
                File.Delete(SessionFile);
        }
        catch { }
        return Task.CompletedTask;
    }
}
