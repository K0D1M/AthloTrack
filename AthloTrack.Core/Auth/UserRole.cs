namespace AthloTrack.Core.Auth;

public enum UserRole
{
    Coach,
    Athlete,

    /// <summary>A login in public.admins: sees only the «Διαχείριση» dashboard.</summary>
    Admin,
}
