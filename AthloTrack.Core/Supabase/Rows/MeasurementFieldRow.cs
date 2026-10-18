using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AthloTrack.Core.Supabase.Rows;

[Table("measurement_fields")]
public sealed class MeasurementFieldRow : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("coach_id")]
    public Guid CoachId { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("unit")]
    public string? Unit { get; set; }
}
