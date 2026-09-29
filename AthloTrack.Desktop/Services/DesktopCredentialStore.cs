using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;

namespace AthloTrack.Desktop.Services;

/// <summary>
/// Desktop-only session storage: the session JSON is encrypted with Windows DPAPI
/// (tied to the current Windows user account) before being written to disk.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DesktopCredentialStore : ICredentialStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AthloTrack", "session.bin");

    private sealed record StoredSession(string AccessToken, string RefreshToken);

    public Task SaveSessionAsync(string accessToken, string refreshToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var json = JsonSerializer.SerializeToUtf8Bytes(new StoredSession(accessToken, refreshToken));
        var encrypted = ProtectedData.Protect(json, optionalEntropy: null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(FilePath, encrypted);
        return Task.CompletedTask;
    }

    public Task<(string? AccessToken, string? RefreshToken)> LoadSessionAsync()
    {
        if (!File.Exists(FilePath))
        {
            return Task.FromResult<(string?, string?)>((null, null));
        }

        try
        {
            var encrypted = File.ReadAllBytes(FilePath);
            var json = ProtectedData.Unprotect(encrypted, optionalEntropy: null, DataProtectionScope.CurrentUser);
            var stored = JsonSerializer.Deserialize<StoredSession>(json);
            return Task.FromResult<(string?, string?)>((stored?.AccessToken, stored?.RefreshToken));
        }
        catch
        {
            return Task.FromResult<(string?, string?)>((null, null));
        }
    }

    public Task ClearAsync()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }
        return Task.CompletedTask;
    }
}
