using System;
using System.Collections.Generic;
using System.Linq;

namespace AthloTrack.Services;

/// <summary>
/// A tapped phone notification asks the shell to open a section. The request is kept until the
/// main view exists, since on a cold start the tap arrives before sign-in/session restore.
/// </summary>
public static class NotificationNavigation
{
    /// <summary>Προπονήσεις, in MainView's drawer order.</summary>
    public const int WorkoutsSection = 2;

    /// <summary>The section to open, and the tapped notification(s) (marked read: they've been seen).</summary>
    public sealed record Request(int Section, IReadOnlyList<Guid> NotificationIds);

    private static Request? _pending;

    /// <summary>Raised when a request arrives; the main view then calls <see cref="TakePending"/>.</summary>
    public static event Action? Requested;

    /// <summary>
    /// Called with the push's data: <c>type</c> (notifications.type) and <c>notificationId</c>,
    /// which is a comma-separated list when an Android group summary was tapped.
    /// </summary>
    public static void RequestFor(string? type, string? notificationIds = null)
    {
        if (type is not ("new_workout" or "workout_updated" or "workout_completed")) return;
        var ids = (notificationIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s, out var id) ? id : (Guid?)null)
            .OfType<Guid>()
            .ToList();
        _pending = new Request(WorkoutsSection, ids);
        Requested?.Invoke();
    }

    public static Request? TakePending()
    {
        var request = _pending;
        _pending = null;
        return request;
    }
}
