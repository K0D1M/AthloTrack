using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AthloTrack.Core.Supabase.Rows;

[Table("measurements")]
public sealed class MeasurementRow : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("athlete_id")]
    public Guid AthleteId { get; set; }

    [Column("measured_at")]
    public DateTime MeasuredAt { get; set; }

    [Column("weight_kg")]
    public decimal WeightKg { get; set; }

    [Column("fat_mass_wt")]
    public decimal? FatMassWt { get; set; }

    [Column("fat_hgt")]
    public decimal? FatHgt { get; set; }

    [Column("created_by")]
    public Guid? CreatedBy { get; set; }

    // Set by the database: sending the model's default would store 0001-01-01.
    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTimeOffset CreatedAt { get; set; }
}
