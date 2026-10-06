using AthloTrack.Core.Models.Admin;
using AthloTrack.Core.Supabase;
using AthloTrack.Core.Supabase.Rows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Supabase.Functions.Exceptions;
using Supabase.Postgrest.Exceptions;

namespace AthloTrack.Core.Data;

public sealed class AdminRepository : IAdminRepository
{
    private readonly SupabaseClientFactory _factory;

    public AdminRepository(SupabaseClientFactory factory)
    {
        _factory = factory;
    }

    public async Task<AdminProfile?> GetByAuthUserIdAsync(Guid authUserId)
    {
        var client = await _factory.GetClientAsync();
        AdminRow? row;
        try
        {
            row = await client.From<AdminRow>().Where(x => x.AuthUserId == authUserId).Single();
        }
        catch (PostgrestException ex)
        {
            // E.g. 011_admin.sql not run yet (no admins table): nobody is an admin. Never let this
            // lookup break sign-in for coaches and athletes.
            Console.WriteLine($"[AthloTrack] Admin lookup failed: {ex.Message}");
            return null;
        }

        return row is null
            ? null
            : new AdminProfile { Id = row.Id, AuthUserId = row.AuthUserId, FullName = row.FullName, Email = row.Email };
    }

    public Task<AdminOverview> GetOverviewAsync() => RpcAsync<AdminOverview>("admin_overview");

    public async Task<IReadOnlyList<ActivityItem>> GetActivityAsync(int limit = 50) =>
        await RpcAsync<List<ActivityItem>>("admin_activity", new() { ["p_limit"] = limit });

    public Task<HealthReport> GetHealthAsync() => RpcAsync<HealthReport>("admin_health");

    public async Task<IReadOnlyList<AdminCoach>> GetCoachesAsync() =>
        await RpcAsync<List<AdminCoach>>("admin_coaches");

    public async Task<IReadOnlyList<AdminAthlete>> GetAthletesAsync() =>
        await RpcAsync<List<AdminAthlete>>("admin_athletes");

    public async Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync() =>
        await RpcAsync<List<DeviceInfo>>("admin_devices");

    public async Task<IReadOnlyList<AuditEntry>> GetAuditLogAsync(int limit = 100) =>
        await RpcAsync<List<AuditEntry>>("admin_audit_log", new() { ["p_limit"] = limit });

    public Task ForcePasswordChangeAsync(Guid coachId) =>
        RpcVoidAsync("admin_force_password_change", new() { ["p_coach"] = coachId });

    public Task MoveAthleteAsync(Guid athleteId, Guid coachId) =>
        RpcVoidAsync("admin_move_athlete", new() { ["p_athlete"] = athleteId, ["p_coach"] = coachId });

    public Task DeleteAthleteAsync(Guid athleteId) =>
        RpcVoidAsync("admin_delete_athlete", new() { ["p_athlete"] = athleteId });

    public Task<AdminActionResult> CreateCoachAsync(string email, string fullName) =>
        InvokeAsync(new() { ["action"] = "create_coach", ["email"] = email, ["full_name"] = fullName });

    public Task<AdminActionResult> ResetPasswordAsync(Guid authUserId) =>
        InvokeAsync(new() { ["action"] = "reset_password", ["auth_user_id"] = authUserId.ToString() });

    public Task DeleteLoginAsync(Guid authUserId) =>
        InvokeAsync(new() { ["action"] = "delete_login", ["auth_user_id"] = authUserId.ToString() });

    public Task<AdminActionResult> SendTestPushAsync(Guid authUserId) =>
        InvokeAsync(new() { ["action"] = "test_push", ["auth_user_id"] = authUserId.ToString() });

    public Task<AdminActionResult> BroadcastAsync(BroadcastAudience audience, string title, string body) =>
        InvokeAsync(new()
        {
            ["action"] = "broadcast",
            ["audience"] = audience switch
            {
                BroadcastAudience.Coaches => "coaches",
                BroadcastAudience.Athletes => "athletes",
                _ => "all",
            },
            ["title"] = title,
            ["body"] = body,
        });

    // ---- plumbing ----

    private async Task<T> RpcAsync<T>(string function, Dictionary<string, object>? args = null) where T : new()
    {
        var client = await _factory.GetClientAsync();
        try
        {
            var response = await client.Rpc(function, args ?? new Dictionary<string, object>());
            return string.IsNullOrWhiteSpace(response.Content)
                ? new T()
                : JsonConvert.DeserializeObject<T>(response.Content) ?? new T();
        }
        catch (PostgrestException ex)
        {
            throw new AdminActionException(DescribeDatabaseError(ex), ex);
        }
    }

    private async Task RpcVoidAsync(string function, Dictionary<string, object> args)
    {
        var client = await _factory.GetClientAsync();
        try
        {
            await client.Rpc(function, args);
        }
        catch (PostgrestException ex)
        {
            throw new AdminActionException(DescribeDatabaseError(ex), ex);
        }
    }

    private async Task<AdminActionResult> InvokeAsync(Dictionary<string, object> body)
    {
        var client = await _factory.GetClientAsync();
        var token = client.Auth.CurrentSession?.AccessToken;
        try
        {
            var json = await client.Functions.Invoke("admin", token,
                new global::Supabase.Functions.Client.InvokeFunctionOptions { Body = body });
            return JsonConvert.DeserializeObject<AdminActionResult>(json) ?? new AdminActionResult();
        }
        catch (FunctionsException ex)
        {
            // The function answers { error: "…" }.
            string? message = null;
            try
            {
                message = JObject.Parse(ex.Content ?? "{}")["error"]?.ToString();
            }
            catch (JsonException)
            {
            }

            if (ex.StatusCode == 404) message = "Η λειτουργία «admin» δεν έχει εγκατασταθεί στο Supabase.";
            throw new AdminActionException(message ?? ex.Message, ex);
        }
    }

    private static string DescribeDatabaseError(PostgrestException ex) =>
        ex.Message.Contains("not admin") ? "Δεν έχεις δικαιώματα διαχειριστή."
        : ex.Message.Contains("PGRST202") ? "Λείπουν οι συναρτήσεις διαχείρισης: τρέξε το 011_admin.sql στο Supabase."
        : ex.Message;
}
