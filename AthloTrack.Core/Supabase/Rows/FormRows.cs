using Newtonsoft.Json.Linq;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AthloTrack.Core.Supabase.Rows;

// 018: «Έντυπα».
[Table("forms")]
public sealed class FormRow : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("coach_id")]
    public Guid CoachId { get; set; }

    // Filled by the database from coaches.full_name.
    [Column("coach_name", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public string CoachName { get; set; } = string.Empty;

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("intro")]
    public string Intro { get; set; } = string.Empty;

    [Column("questions")]
    public JArray Questions { get; set; } = new();

    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("updated_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTimeOffset UpdatedAt { get; set; }
}

[Table("form_assignments")]
public sealed class FormAssignmentRow : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("form_id")]
    public Guid FormId { get; set; }

    [Column("athlete_id")]
    public Guid AthleteId { get; set; }

    // Written only by submit_form.
    [Column("answers", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public JObject? Answers { get; set; }

    [Column("submitted_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTimeOffset? SubmittedAt { get; set; }

    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTimeOffset CreatedAt { get; set; }
}
