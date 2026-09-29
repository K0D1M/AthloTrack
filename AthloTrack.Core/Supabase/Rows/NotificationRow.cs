using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AthloTrack.Core.Supabase.Rows;

[Table("notifications")]
public sealed class NotificationRow : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("athlete_id")]
    public Guid AthleteId { get; set; }

    [Column("type")]
    public string Type { get; set; } = "new_workout";

    [Column("message")]
    public string Message { get; set; } = string.Empty;

    [Column("related_workout_id")]
    public Guid? RelatedWorkoutId { get; set; }

    [Column("is_read")]
    public bool IsRead { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
}
