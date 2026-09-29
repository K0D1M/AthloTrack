namespace FitTrack.Core.Models;

public sealed class Measurement
{
    public Guid Id { get; set; }
    public Guid AthleteId { get; set; }
    public DateOnly MeasuredAt { get; set; }
    public decimal WeightKg { get; set; }
    public decimal? FatMassWt { get; set; }
    public decimal? FatHgt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
