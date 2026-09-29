namespace FitTrack.Core.Models;

public sealed class Coach
{
    public Guid Id { get; set; }
    public Guid AuthUserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
