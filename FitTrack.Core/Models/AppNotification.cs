namespace FitTrack.Core.Models;

public sealed class AppNotification
{
    public Guid Id { get; set; }
    public Guid AthleteId { get; set; }
    public string Type { get; set; } = "new_workout";
    public string Message { get; set; } = string.Empty;
    public Guid? RelatedWorkoutId { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
