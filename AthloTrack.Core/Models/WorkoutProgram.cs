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

    /// <summary>The coach can ask the athlete about it: open, and not reminded in the last hour.</summary>
    public bool CanBeReminded => !IsAnswered && !ReminderRecentlySent;

    /// <summary>Show «Υπενθύμιση …» to the coach: a reminder was sent and there's no answer yet.</summary>
    public bool ShowReminderSent => LastRemindedAt is not null && !IsAnswered;

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
