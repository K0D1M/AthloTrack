using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AthloTrack.Core.Supabase.Rows;

[Table("workout_templates")]
public sealed class WorkoutTemplateRow : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("coach_id")]
    public Guid CoachId { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("content")]
    public string Content { get; set; } = string.Empty;

    // Set by the database.
    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTimeOffset CreatedAt { get; set; }
}
