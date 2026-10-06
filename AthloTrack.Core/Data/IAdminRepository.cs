using AthloTrack.Core.Models.Admin;

namespace AthloTrack.Core.Data;

/// <summary>
/// The admin dashboard's data. Reads and simple changes go through the admin_* database functions
/// (supabase/011_admin.sql); logins and pushes through the "admin" Edge Function. Both refuse
/// anyone who isn't in public.admins.
/// </summary>
public interface IAdminRepository
{
    /// <summary>The admins row of this login, or null (RLS: an admin sees only their own row).</summary>
    Task<AdminProfile?> GetByAuthUserIdAsync(Guid authUserId);

    Task<AdminOverview> GetOverviewAsync();
    Task<IReadOnlyList<ActivityItem>> GetActivityAsync(int limit = 50);
    Task<HealthReport> GetHealthAsync();
    Task<IReadOnlyList<AdminCoach>> GetCoachesAsync();
    Task<IReadOnlyList<AdminAthlete>> GetAthletesAsync();
    Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync();
    Task<IReadOnlyList<AuditEntry>> GetAuditLogAsync(int limit = 100);

    Task ForcePasswordChangeAsync(Guid coachId);
    Task MoveAthleteAsync(Guid athleteId, Guid coachId);
    Task DeleteAthleteAsync(Guid athleteId);

    /// <summary>A login and coach profile; the result carries the temporary password.</summary>
    Task<AdminActionResult> CreateCoachAsync(string email, string fullName);

    /// <summary>A new temporary password (coaches must then choose their own).</summary>
    Task<AdminActionResult> ResetPasswordAsync(Guid authUserId);

    /// <summary>Deletes the login; a coach's athletes go with it (cascade).</summary>
    Task DeleteLoginAsync(Guid authUserId);

    Task<AdminActionResult> SendTestPushAsync(Guid authUserId);
    Task<AdminActionResult> BroadcastAsync(BroadcastAudience audience, string title, string body);
}
