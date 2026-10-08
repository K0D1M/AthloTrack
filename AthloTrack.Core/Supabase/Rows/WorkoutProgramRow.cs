using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AthloTrack.Core.Supabase.Rows;

[Table("workout_programs")]
public sealed class WorkoutProgramRow : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("athlete_id")]
    public Guid AthleteId { get; set; }

    [Column("title")]
    public string? Title { get; set; }

    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("target_date")]
    public DateTime TargetDate { get; set; }

    [Column("created_by")]
    public Guid? CreatedBy { get; set; }

    [Column("completed_at")]
    public DateTimeOffset? CompletedAt { get; set; }

    [Column("coach_present")]
    public bool? CoachPresent { get; set; }

    [Column("read_at")]
    public DateTimeOffset? ReadAt { get; set; }

    // Read-only in the row model: written only through MarkNotCompletedAsync / remind_workout, so
    // saving a whole row (UpdateAsync) never sends them (and works before 012_….sql is run).
    [Column("not_completed_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTimeOffset? NotCompletedAt { get; set; }

    [Column("last_reminded_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTimeOffset? LastRemindedAt { get; set; }

    // Set by the database: sending the model's default would store 0001-01-01.
    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTimeOffset CreatedAt { get; set; }
}
