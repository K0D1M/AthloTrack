namespace AthloTrack.Core.Models;

public sealed class AppNotification
{
    public Guid Id { get; set; }
    public Guid AthleteId { get; set; }

    /// <summary>"athlete" or "coach".</summary>
    public string Recipient { get; set; } = "athlete";
    public string Type { get; set; } = "new_workout";
    public string Message { get; set; } = string.Empty;
    public Guid? RelatedWorkoutId { get; set; }

    /// <summary>018: the «Έντυπο» copy (new_form / form_submitted).</summary>
    public Guid? RelatedFormAssignmentId { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
