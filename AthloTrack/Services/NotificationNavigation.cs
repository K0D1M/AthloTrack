using System;

namespace AthloTrack.Services;

/// <summary>
/// A tapped phone notification asks the shell to open a section. The request is kept until the
/// main view exists, since on a cold start the tap arrives before sign-in/session restore.
/// </summary>
public static class NotificationNavigation
{
    /// <summary>Προπονήσεις, in MainView's drawer order.</summary>
    public const int WorkoutsSection = 2;

    private static int? _pending;

    /// <summary>Raised when a request arrives; the main view then calls <see cref="TakePending"/>.</summary>
    public static event Action? Requested;

    /// <summary>Called with the push's <c>type</c> (the notifications.type column).</summary>
    public static void RequestFor(string? type)
    {
        if (type is not ("new_workout" or "workout_completed")) return;
        _pending = WorkoutsSection;
        Requested?.Invoke();
    }

    public static int? TakePending()
    {
        var section = _pending;
        _pending = null;
        return section;
    }
}
