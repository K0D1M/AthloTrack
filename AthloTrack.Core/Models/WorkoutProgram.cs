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

    /// <summary>When the athlete first saw it (read receipt for the coach); null while unseen.</summary>
    public DateTimeOffset? ReadAt { get; set; }

    public bool IsRead => ReadAt is not null;

    /// <summary>ReadAt in the device's time zone, for display.</summary>
    public DateTimeOffset? ReadAtLocal => ReadAt?.ToLocalTime();
}
