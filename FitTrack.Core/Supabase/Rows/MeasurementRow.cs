using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace FitTrack.Core.Supabase.Rows;

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

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
}
