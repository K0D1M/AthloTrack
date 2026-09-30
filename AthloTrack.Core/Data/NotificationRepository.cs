using AthloTrack.Core.Models;
using AthloTrack.Core.Supabase;
using AthloTrack.Core.Supabase.Rows;
using Supabase.Postgrest;

namespace AthloTrack.Core.Data;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly SupabaseClientFactory _factory;

    public NotificationRepository(SupabaseClientFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<AppNotification>> GetUnreadForAthleteAsync(Guid athleteId)
    {
        var client = await _factory.GetClientAsync();
        var response = await client.From<NotificationRow>()
            .Where(x => x.AthleteId == athleteId && x.Recipient == "athlete" && x.IsRead == false)
            .Order(x => x.CreatedAt, Constants.Ordering.Descending)
            .Get();
        return response.Models.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<AppNotification>> GetUnreadForCoachAsync()
    {
        // RLS limits the rows to the signed-in coach's own athletes.
        var client = await _factory.GetClientAsync();
        var response = await client.From<NotificationRow>()
            .Where(x => x.Recipient == "coach" && x.IsRead == false)
            .Order(x => x.CreatedAt, Constants.Ordering.Descending)
            .Get();
        return response.Models.Select(Map).ToList();
    }

    public async Task MarkReadAsync(Guid notificationId)
    {
        var client = await _factory.GetClientAsync();
        await client.From<NotificationRow>()
            .Where(x => x.Id == notificationId)
            .Set(x => x.IsRead, true)
            .Update();
    }

    private static AppNotification Map(NotificationRow r) => new()
    {
        Id = r.Id,
        AthleteId = r.AthleteId,
        Recipient = r.Recipient,
        Type = r.Type,
        Message = r.Message,
        RelatedWorkoutId = r.RelatedWorkoutId,
        IsRead = r.IsRead,
        CreatedAt = r.CreatedAt,
    };
}
