namespace AthloTrack.Core.Models;

/// <summary>A coach's own measurement field for «Άλλες μετρήσεις», e.g. «Περιφέρεια μέσης» (cm).</summary>
public sealed class MeasurementField
{
    public Guid Id { get; set; }
    public Guid CoachId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Unit { get; set; }

    /// <summary>«Περιφέρεια μέσης (cm)», or just the name without a unit.</summary>
    public string Label => string.IsNullOrWhiteSpace(Unit) ? Name : $"{Name} ({Unit})";
}
