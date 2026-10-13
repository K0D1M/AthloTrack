namespace AthloTrack.Core.Models;

public sealed class WorkoutProgram
{
    public Guid Id { get; set; }
    public Guid AthleteId { get; set; }
    public string? Title { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateOnly TargetDate { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The athlete's «Θα το κάνω»; null until they choose (013 backfills older workouts).</summary>
    public DateTimeOffset? AcceptedAt { get; set; }

    /// <summary>The athlete's «Δεν θα το κάνω» (final); null otherwise.</summary>
    public DateTimeOffset? DeclinedAt { get; set; }

    /// <summary>The athlete's optional reason for declining.</summary>
    public string? DeclineReason { get; set; }

    /// <summary>When the coach cancelled it; null while it stands (the coach can restore it).</summary>
    public DateTimeOffset? CancelledAt { get; set; }

    public bool IsAccepted => AcceptedAt is not null;
    public bool IsDeclined => DeclinedAt is not null;
    public bool IsCancelled => CancelledAt is not null;
    public bool HasDeclineReason => IsDeclined && !string.IsNullOrWhiteSpace(DeclineReason);

    /// <summary>Waiting for the athlete's «Θα το κάνω / Δεν θα το κάνω».</summary>
    public bool IsPending => !IsAccepted && !IsDeclined && !IsCancelled;

    /// <summary>Accepted and open: the athlete can give the final «Ολοκληρώθηκε / Δεν ολοκληρώθηκε».</summary>
    public bool CanAnswerOutcome => IsAccepted && !IsDeclined && !IsCancelled && !IsAnswered;

    /// <summary>Nothing more is expected of the athlete: cancelled, declined or answered.</summary>
    public bool IsClosed => IsCancelled || IsDeclined || IsAnswered;

    /// <summary>The coach can cancel it: not already cancelled, declined or answered.</summary>
    public bool CanBeCancelled => !IsClosed;

    /// <summary>Accepted and still open (the «✓ Θα το κάνει» line).</summary>
    public bool ShowAccepted => IsAccepted && !IsCancelled && !IsAnswered;

    public DateTimeOffset? CancelledAtLocal => CancelledAt?.ToLocalTime();

    /// <summary>When the athlete marked it done; null while open.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    public bool IsCompleted => CompletedAt is not null;

    /// <summary>When the athlete answered «Δεν ολοκληρώθηκε»; null otherwise.</summary>
    public DateTimeOffset? NotCompletedAt { get; set; }

    public bool IsNotCompleted => NotCompletedAt is not null;

    /// <summary>The athlete gave either answer; it's final.</summary>
    public bool IsAnswered => CompletedAt is not null || NotCompletedAt is not null;

    /// <summary>When the coach last sent a reminder (at most once an hour).</summary>
    public DateTimeOffset? LastRemindedAt { get; set; }

    /// <summary>A reminder went out less than an hour ago: the coach can't send another yet.</summary>
    public bool ReminderRecentlySent => LastRemindedAt is { } at && DateTimeOffset.UtcNow - at < TimeSpan.FromHours(1);

    /// <summary>The coach can ask the athlete about it: not closed, and not reminded in the last hour.</summary>
    public bool CanBeReminded => !IsClosed && !ReminderRecentlySent;

    /// <summary>Show «Υπενθύμιση …» to the coach: a reminder was sent and it isn't closed yet.</summary>
    public bool ShowReminderSent => LastRemindedAt is not null && !IsClosed;

    public DateTimeOffset? LastRemindedAtLocal => LastRemindedAt?.ToLocalTime();
    public DateTimeOffset? CompletedAtLocal => CompletedAt?.ToLocalTime();
    public DateTimeOffset? NotCompletedAtLocal => NotCompletedAt?.ToLocalTime();

    /// <summary>The coach's word on being at this workout: true Παρών, false Απών, null not said.</summary>
    public bool? CoachPresent { get; set; }

    /// <summary>When the athlete first saw it (read receipt for the coach); null while unseen.</summary>
    public DateTimeOffset? ReadAt { get; set; }

    public bool IsRead => ReadAt is not null;

    /// <summary>ReadAt in the device's time zone, for display.</summary>
    public DateTimeOffset? ReadAtLocal => ReadAt?.ToLocalTime();
}
