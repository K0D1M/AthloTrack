namespace FitTrack.Core.Models;

public sealed class Athlete
{
    public Guid Id { get; set; }
    public Guid AuthUserId { get; set; }
    public Guid CoachId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public decimal? HeightCm { get; set; }
    public string? ProfileImagePath { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
