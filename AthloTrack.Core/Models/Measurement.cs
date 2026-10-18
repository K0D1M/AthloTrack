namespace AthloTrack.Core.Models;

public sealed class Measurement
{
    public Guid Id { get; set; }
    public Guid AthleteId { get; set; }
    public DateOnly MeasuredAt { get; set; }
    public decimal WeightKg { get; set; }
    public decimal? FatMassWt { get; set; }
    public decimal? FatHgt { get; set; }

    /// <summary>«Άλλες μετρήσεις»: the value of each of the coach's own fields (by field id).</summary>
    public Dictionary<Guid, decimal> Extra { get; set; } = new();

    /// <summary>
    /// For display only (not stored): «Περιφέρεια μέσης: 82 cm» per custom value, filled by the
    /// profile once the field names are known.
    /// </summary>
    public List<string> ExtraLines { get; set; } = new();

    /// <summary>«Λιπομέτρηση» has a value.</summary>
    public bool HasFat => FatMassWt is not null || FatHgt is not null;
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
