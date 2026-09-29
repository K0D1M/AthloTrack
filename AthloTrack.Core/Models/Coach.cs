namespace AthloTrack.Core.Models;

public sealed class Coach
{
    public Guid Id { get; set; }
    public Guid AuthUserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? ProfileImagePath { get; set; }

    /// <summary>Set by the admin for new logins: choose an own password before using the app.</summary>
    public bool MustSetPassword { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
