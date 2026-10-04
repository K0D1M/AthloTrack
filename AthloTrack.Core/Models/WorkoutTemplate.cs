namespace AthloTrack.Core.Models;

/// <summary>A coach's saved workout text, to start new workouts from.</summary>
public sealed class WorkoutTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
