using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;

namespace AthloTrack.WinUI.Services;

/// <summary>
/// DPAPI-encrypted session storage kept in its own folder (AthloTrackWinUI) so this head
/// signs in independently of the Avalonia app and neither can corrupt the other's file.
/// </summary>
public sealed class WinUiCredentialStore : ICredentialStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AthloTrack.WinUI.v1");

    private sealed record Stored(string AccessToken, string RefreshToken);

    private static string FilePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AthloTrackWinUI");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "session.bin");
        }
    }

    public Task SaveSessionAsync(string accessToken, string refreshToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(new Stored(accessToken, refreshToken));
            var cipher = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(json), Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(FilePath, cipher);
        }
        catch
        {
            // Non-fatal: the user just signs in again next launch.
        }
        return Task.CompletedTask;
    }

    public Task<(string? AccessToken, string? RefreshToken)> LoadSessionAsync()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var plain = ProtectedData.Unprotect(
                    File.ReadAllBytes(FilePath), Entropy, DataProtectionScope.CurrentUser);
                var stored = JsonSerializer.Deserialize<Stored>(Encoding.UTF8.GetString(plain));
                if (stored is not null)
                    return Task.FromResult<(string?, string?)>((stored.AccessToken, stored.RefreshToken));
            }
        }
        catch
        {
            // fall through to empty
        }
        return Task.FromResult<(string?, string?)>((null, null));
    }

    public Task ClearAsync()
    {
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
        catch
        {
            // ignore
        }
        return Task.CompletedTask;
    }
}
