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
}
