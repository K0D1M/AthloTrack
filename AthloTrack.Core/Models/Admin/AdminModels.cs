using Newtonsoft.Json;

namespace AthloTrack.Core.Models.Admin;

// What the admin_* database functions (supabase/011_admin.sql) and the "admin" Edge Function
// return. Property names follow the JSON keys.

public sealed class AdminProfile
{
    public Guid Id { get; set; }
    public Guid AuthUserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
}

/// <summary>admin_overview(): counts across the whole app.</summary>
public sealed class AdminOverview
{
    [JsonProperty("coaches")] public int Coaches { get; set; }
    [JsonProperty("athletes")] public int Athletes { get; set; }
    [JsonProperty("linked_athletes")] public int LinkedAthletes { get; set; }
    [JsonProperty("logins")] public int Logins { get; set; }
    [JsonProperty("active_7d")] public int Active7Days { get; set; }
    [JsonProperty("active_30d")] public int Active30Days { get; set; }
    [JsonProperty("app_users")] public int AppUsers { get; set; }
    [JsonProperty("workouts_total")] public int WorkoutsTotal { get; set; }
    [JsonProperty("workouts_week")] public int WorkoutsThisWeek { get; set; }
    [JsonProperty("workouts_month")] public int WorkoutsThisMonth { get; set; }

    /// <summary>Workouts with a target date in the last 30 days, and how many of them were done / read.</summary>
    [JsonProperty("due_30d")] public int Due30Days { get; set; }
    [JsonProperty("completed_30d")] public int Completed30Days { get; set; }
    [JsonProperty("read_30d")] public int Read30Days { get; set; }

    [JsonProperty("measurements_month")] public int MeasurementsThisMonth { get; set; }
}

/// <summary>One event in admin_activity().</summary>
public sealed class ActivityItem
{
    /// <summary>"notification:new_workout", "notification:workout_completed", "measurement", "athlete", "signup", …</summary>
    [JsonProperty("kind")] public string Kind { get; set; } = string.Empty;
    [JsonProperty("at")] public DateTimeOffset At { get; set; }
    [JsonProperty("who")] public string Who { get; set; } = string.Empty;
    [JsonProperty("text")] public string Text { get; set; } = string.Empty;
}

/// <summary>One row of a health-check list.</summary>
public sealed class HealthItem
{
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("detail")] public string Detail { get; set; } = string.Empty;
}

/// <summary>admin_health(): one list per check.</summary>
public sealed class HealthReport
{
    [JsonProperty("orphan_logins")] public List<HealthItem> OrphanLogins { get; set; } = new();
    [JsonProperty("athletes_no_email")] public List<HealthItem> AthletesNoEmail { get; set; } = new();
    [JsonProperty("athletes_unlinked")] public List<HealthItem> AthletesUnlinked { get; set; } = new();
    [JsonProperty("athletes_inactive")] public List<HealthItem> AthletesInactive { get; set; } = new();
    [JsonProperty("coaches_pending_password")] public List<HealthItem> CoachesPendingPassword { get; set; } = new();
    [JsonProperty("orphan_devices")] public List<HealthItem> OrphanDevices { get; set; } = new();
}

public sealed class AdminCoach
{
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("auth_user_id")] public Guid? AuthUserId { get; set; }
    [JsonProperty("full_name")] public string FullName { get; set; } = string.Empty;
    [JsonProperty("email")] public string? Email { get; set; }
    [JsonProperty("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [JsonProperty("must_set_password")] public bool MustSetPassword { get; set; }
    [JsonProperty("athletes")] public int Athletes { get; set; }
    [JsonProperty("last_sign_in_at")] public DateTimeOffset? LastSignInAt { get; set; }
    [JsonProperty("has_app")] public bool HasApp { get; set; }
}

public sealed class AdminAthlete
{
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("auth_user_id")] public Guid? AuthUserId { get; set; }
    [JsonProperty("full_name")] public string FullName { get; set; } = string.Empty;
    [JsonProperty("email")] public string? Email { get; set; }
    [JsonProperty("coach_id")] public Guid CoachId { get; set; }
    [JsonProperty("coach_name")] public string CoachName { get; set; } = string.Empty;
    [JsonProperty("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [JsonProperty("last_sign_in_at")] public DateTimeOffset? LastSignInAt { get; set; }
    [JsonProperty("last_activity")] public DateTimeOffset LastActivity { get; set; }
    [JsonProperty("workouts")] public int Workouts { get; set; }
    [JsonProperty("measurements")] public int Measurements { get; set; }
    [JsonProperty("has_app")] public bool HasApp { get; set; }

    public bool IsLinked => AuthUserId is not null;
}

/// <summary>A device with the app installed (or a browser that allowed notifications).</summary>
public sealed class DeviceInfo
{
    [JsonProperty("auth_user_id")] public Guid AuthUserId { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("email")] public string? Email { get; set; }

    /// <summary>"coach", "athlete", "admin" or "none".</summary>
    [JsonProperty("role")] public string Role { get; set; } = string.Empty;
    [JsonProperty("platform")] public string Platform { get; set; } = string.Empty;
    [JsonProperty("updated_at")] public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class AuditEntry
{
    [JsonProperty("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [JsonProperty("action")] public string Action { get; set; } = string.Empty;
    [JsonProperty("target")] public string? Target { get; set; }
    [JsonProperty("admin_email")] public string AdminEmail { get; set; } = string.Empty;
}

/// <summary>Who a broadcast goes to.</summary>
public enum BroadcastAudience
{
    All,
    Coaches,
    Athletes,
}

/// <summary>The "admin" Edge Function's answer.</summary>
public sealed class AdminActionResult
{
    /// <summary>create_coach / reset_password: shown once to the admin, never stored by the app.</summary>
    [JsonProperty("temp_password")] public string? TempPassword { get; set; }
    [JsonProperty("sent")] public int Sent { get; set; }
    [JsonProperty("users")] public int Users { get; set; }
}

/// <summary>An admin call the server refused or that failed (the message is shown to the admin).</summary>
public sealed class AdminActionException : Exception
{
    public AdminActionException(string message, Exception? inner = null) : base(message, inner) { }
}
