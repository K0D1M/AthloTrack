using System;
using AthloTrack.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AthloTrack.ViewModels;

/// <summary>
/// A notification on Πρόσφατα: the other person (the athlete for a coach, the coach for an
/// athlete) as a photo with a badge for what happened, their name in bold and the rest of the text.
/// </summary>
public sealed partial class NotificationItem : ObservableObject
{
    public NotificationItem(AppNotification notification, string? personName, DateTimeOffset? now = null)
    {
        Notification = notification;
        (Name, Rest) = Split(notification.Message, personName);
        var initialFrom = string.IsNullOrEmpty(personName) ? notification.Message : personName;
        Initial = initialFrom.Length > 0 ? initialFrom[..1].ToUpperInvariant() : "?";
        WhenText = TimeText.Day(notification.CreatedAt, now);
        Kind = KindOf(notification.Type);
    }

    public AppNotification Notification { get; }

    /// <summary>The person, in bold; empty when the message doesn't name them.</summary>
    public string Name { get; }
    public bool HasName => Name.Length > 0;

    /// <summary>The bold run: the name and a space, or nothing.</summary>
    public string NameLead => HasName ? Name + " " : string.Empty;

    /// <summary>The rest of the message after the name («ολοκλήρωσε το ασκησιολόγιο της 10/11»).</summary>
    public string Rest { get; }

    public string Initial { get; }

    /// <summary>The person's photo; null shows <see cref="Initial"/>.</summary>
    [ObservableProperty]
    public partial byte[]? Photo { get; set; }

    /// <summary>«σήμερα 18:42», «χθες 21:10», «07/10 18:32».</summary>
    public string WhenText { get; }

    /// <summary>The badge: done, notdone, accepted, declined, form, workout, reminder, cancelled, restored.</summary>
    public string Kind { get; }

    // One is true: the badge's colour and icon (styles in Styles/Workouts.axaml).
    public bool IsDone => Kind == "done";
    public bool IsNotDone => Kind == "notdone";
    public bool IsAccepted => Kind == "accepted";
    public bool IsDeclined => Kind == "declined";
    public bool IsForm => Kind == "form";
    public bool IsWorkout => Kind == "workout";
    public bool IsReminder => Kind == "reminder";
    public bool IsCancelled => Kind == "cancelled";
    public bool IsRestored => Kind == "restored";

    public static string KindOf(string type) => type switch
    {
        "workout_completed" => "done",
        "workout_not_completed" => "notdone",
        "workout_accepted" => "accepted",
        "workout_declined" => "declined",
        "new_form" or "form_submitted" => "form",
        "workout_reminder" => "reminder",
        "workout_cancelled" => "cancelled",
        "workout_restored" => "restored",
        _ => "workout", // new_workout, workout_updated
    };

    /// <summary>
    /// «Ο/Η Μαρία Κ. ολοκλήρωσε …» → («Μαρία Κ.», «ολοκλήρωσε …»): the article before the name goes,
    /// as in «Μαρία Κ. ολοκλήρωσε». A message that doesn't start with the name stays whole.
    /// </summary>
    public static (string Name, string After) Split(string message, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return (string.Empty, message);
        var at = message.IndexOf(name, StringComparison.Ordinal);
        if (at < 0) return (string.Empty, message);
        var before = message[..at].Trim();
        if (before is not ("" or "Ο/Η" or "Ο" or "Η")) return (string.Empty, message);
        return (name, message[(at + name.Length)..].Trim());
    }
}
