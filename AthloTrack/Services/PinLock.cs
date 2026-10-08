using System;
using System.Security.Cryptography;
using System.Text;
using AthloTrack.Core.Auth;

namespace AthloTrack.Services;

/// <summary>The result of checking a PIN.</summary>
public enum PinCheckResult
{
    Ok,
    Wrong,

    /// <summary>Too many wrong PINs: the PIN is gone and the user must sign in with the password.</summary>
    LockedOut,
}

public readonly record struct PinCheck(PinCheckResult Result, int Remaining);

/// <summary>
/// «Κλείδωμα με PIN»: a 6-digit PIN that unlocks the saved session on this device. Stored as a
/// salted PBKDF2 hash in the device preferences, tied to the login it was set for.
/// It keeps someone who picks up the unlocked device out; it doesn't encrypt anything (the session
/// itself lives in the platform's credential store).
/// </summary>
public sealed class PinLock
{
    public const int Length = 6;
    public const int MaxAttempts = 5;

    /// <summary>Away from the app longer than this: it asks for the PIN on return.</summary>
    public static readonly TimeSpan LockAfter = TimeSpan.FromMinutes(1);

    private const string UserKey = "pin.user", SaltKey = "pin.salt", HashKey = "pin.hash",
        IterKey = "pin.iter", FailuresKey = "pin.failures";

    // ~0.1–0.3 s per check in the browser, much less natively; 10^6 PINs make it slow to brute-force.
    private const int Iterations = 60_000;

    private readonly IAppPreferences _preferences;
    private DateTimeOffset? _backgroundSince;

    public PinLock(IAppPreferences preferences)
    {
        _preferences = preferences;
    }

    public static bool IsValid(string? pin) =>
        pin is { Length: Length } && pin.AsSpan().IndexOfAnyExceptInRange('0', '9') < 0;

    /// <summary>A PIN is set for this login. One left by another login is removed.</summary>
    public bool IsEnabledFor(Guid? userId)
    {
        var owner = _preferences.Get(UserKey);
        if (owner is null || userId is null) return false;
        if (owner == userId.Value.ToString()) return _preferences.Get(HashKey) is not null;
        Clear();
        return false;
    }

    public void Set(Guid userId, string pin)
    {
        if (!IsValid(pin)) throw new ArgumentException("Το PIN έχει 6 ψηφία.", nameof(pin));
        var salt = RandomNumberGenerator.GetBytes(16);
        _preferences.Set(UserKey, userId.ToString());
        _preferences.Set(SaltKey, Convert.ToBase64String(salt));
        _preferences.Set(IterKey, Iterations.ToString());
        _preferences.Set(HashKey, Convert.ToBase64String(Hash(pin, salt, Iterations)));
        _preferences.Set(FailuresKey, null);
    }

    /// <summary>Checks the PIN; the 5th wrong one in a row removes it (<see cref="PinCheckResult.LockedOut"/>).</summary>
    public PinCheck Verify(string pin)
    {
        var saltText = _preferences.Get(SaltKey);
        var hashText = _preferences.Get(HashKey);
        if (saltText is null || hashText is null) return new PinCheck(PinCheckResult.LockedOut, 0);

        var iterations = int.TryParse(_preferences.Get(IterKey), out var it) ? it : Iterations;
        var expected = Convert.FromBase64String(hashText);
        var actual = IsValid(pin) ? Hash(pin, Convert.FromBase64String(saltText), iterations) : Array.Empty<byte>();
        if (actual.Length == expected.Length && CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            _preferences.Set(FailuresKey, null);
            return new PinCheck(PinCheckResult.Ok, MaxAttempts);
        }

        var failures = (int.TryParse(_preferences.Get(FailuresKey), out var f) ? f : 0) + 1;
        if (failures >= MaxAttempts)
        {
            Clear();
            return new PinCheck(PinCheckResult.LockedOut, 0);
        }
        _preferences.Set(FailuresKey, failures.ToString());
        return new PinCheck(PinCheckResult.Wrong, MaxAttempts - failures);
    }

    public void Clear()
    {
        foreach (var key in new[] { UserKey, SaltKey, HashKey, IterKey, FailuresKey }) _preferences.Set(key, null);
    }

    // ---- Background ----

    /// <summary>The app went to the background (or lost focus) at <paramref name="now"/>.</summary>
    public void NoteBackground(DateTimeOffset now) => _backgroundSince ??= now;

    /// <summary>Back in front: true when it was away longer than <see cref="LockAfter"/>.</summary>
    public bool ShouldLockOnReturn(DateTimeOffset now)
    {
        var since = _backgroundSince;
        _backgroundSince = null;
        return since is not null && now - since.Value > LockAfter;
    }

    private static byte[] Hash(string pin, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(pin), salt, iterations, HashAlgorithmName.SHA256, 32);
}
