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

    /// <summary>The section to open, and the tapped notification (marked read: it has been seen).</summary>
    public sealed record Request(int Section, Guid? NotificationId);

    private static Request? _pending;

    /// <summary>Raised when a request arrives; the main view then calls <see cref="TakePending"/>.</summary>
    public static event Action? Requested;

    /// <summary>Called with the push's data: <c>type</c> (notifications.type) and <c>notificationId</c>.</summary>
    public static void RequestFor(string? type, string? notificationId = null)
    {
        if (type is not ("new_workout" or "workout_updated" or "workout_completed")) return;
        _pending = new Request(WorkoutsSection, Guid.TryParse(notificationId, out var id) ? id : null);
        Requested?.Invoke();
    }

    public static Request? TakePending()
    {
        var request = _pending;
        _pending = null;
        return request;
    }
}
