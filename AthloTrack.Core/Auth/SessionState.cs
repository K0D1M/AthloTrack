namespace AthloTrack.Core.Auth;

/// <summary>Holds the currently signed-in user's identity and role for the app session.</summary>
public sealed class SessionState
{
    public Guid? AuthUserId { get; set; }
    public UserRole? Role { get; set; }

    /// <summary>The coaches.id or athletes.id row matching the signed-in auth user.</summary>
    public Guid? ProfileId { get; set; }

    public string? DisplayName { get; set; }

    /// <summary>Storage path of the signed-in user's own photo (coach or athlete).</summary>
    public string? ProfileImagePath { get; set; }

    /// <summary>For athletes: their coach's coaches.id.</summary>
    public Guid? CoachId { get; set; }

    /// <summary>Coach signed in with an admin-issued password and must choose their own.</summary>
    public bool MustSetPassword { get; set; }

    public bool IsCoach => Role == UserRole.Coach;

    public void Clear()
    {
        AuthUserId = null;
        Role = null;
        ProfileId = null;
        DisplayName = null;
        ProfileImagePath = null;
        CoachId = null;
        MustSetPassword = false;
    }
}
