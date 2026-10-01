using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Android.Security.Keystore;
using AthloTrack.Core.Auth;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;

namespace AthloTrack.Android.Services;

/// <summary>
/// Android session storage: the session JSON is encrypted (AES-256-GCM) with a key that lives in
/// the Android Keystore and never leaves it, then written to the app's private files directory.
/// </summary>
public sealed class AndroidCredentialStore : ICredentialStore
{
    private const string KeyStoreName = "AndroidKeyStore";
    private const string KeyAlias = "athlotrack_session";
    private const int TagBits = 128;

    private static string Dir => global::Android.App.Application.Context.FilesDir!.AbsolutePath;
    private static string SessionFile => Path.Combine(Dir, "session.bin");

    /// <summary>Plain-text file written by versions before the session was encrypted.</summary>
    private static string LegacyFile => Path.Combine(Dir, "session.json");

    private sealed record StoredSession(string AccessToken, string RefreshToken);

    public Task SaveSessionAsync(string accessToken, string refreshToken)
    {
        var json = JsonSerializer.Serialize(new StoredSession(accessToken, refreshToken));
        File.WriteAllText(SessionFile, Encrypt(json));
        DeleteQuietly(LegacyFile);
        return Task.CompletedTask;
    }

    public async Task<(string? AccessToken, string? RefreshToken)> LoadSessionAsync()
    {
        try
        {
            if (File.Exists(SessionFile))
            {
                var session = JsonSerializer.Deserialize<StoredSession>(Decrypt(File.ReadAllText(SessionFile)));
                return (session?.AccessToken, session?.RefreshToken);
            }

            // Upgrade from the plain-text file: re-save encrypted, so the user stays signed in.
            if (File.Exists(LegacyFile))
            {
                var session = JsonSerializer.Deserialize<StoredSession>(File.ReadAllText(LegacyFile));
                DeleteQuietly(LegacyFile);
                if (session is not null)
                {
                    await SaveSessionAsync(session.AccessToken, session.RefreshToken);
                    return (session.AccessToken, session.RefreshToken);
                }
            }
        }
        catch (Exception ex)
        {
            // Unreadable (e.g. the Keystore key was reset): sign in again.
            Console.WriteLine($"[AthloTrack] Stored session unreadable: {ex.GetType().Name}");
            DeleteQuietly(SessionFile);
        }
        return (null, null);
    }

    public Task ClearAsync()
    {
        DeleteQuietly(SessionFile);
        DeleteQuietly(LegacyFile);
        return Task.CompletedTask;
    }

    private static string Encrypt(string plain)
    {
        var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
        cipher.Init(CipherMode.EncryptMode, GetOrCreateKey());
        var iv = cipher.GetIV()!;
        var data = cipher.DoFinal(Encoding.UTF8.GetBytes(plain))!;
        return $"v1:{Convert.ToBase64String(iv)}:{Convert.ToBase64String(data)}";
    }

    private static string Decrypt(string stored)
    {
        var parts = stored.Split(':');
        if (parts.Length != 3 || parts[0] != "v1") throw new InvalidDataException("Unknown session format.");
        var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
        cipher.Init(CipherMode.DecryptMode, GetOrCreateKey(), new GCMParameterSpec(TagBits, Convert.FromBase64String(parts[1])));
        return Encoding.UTF8.GetString(cipher.DoFinal(Convert.FromBase64String(parts[2]))!);
    }

    private static IKey GetOrCreateKey()
    {
        var store = KeyStore.GetInstance(KeyStoreName)!;
        store.Load(null);
        if (store.GetKey(KeyAlias, null) is { } existing) return existing;

        var generator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, KeyStoreName)!;
        generator.Init(new KeyGenParameterSpec.Builder(KeyAlias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
            .SetBlockModes(KeyProperties.BlockModeGcm)
            .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)
            .SetKeySize(256)
            .Build());
        return generator.GenerateKey()!;
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // nothing to clean up
        }
    }
}
