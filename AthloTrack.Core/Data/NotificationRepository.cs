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
            // One filter per Where: Postgrest-csharp turns a three-term && into a nested
            // and-tree PostgREST can't parse (PGRST100), which failed the whole athlete home.
            .Where(x => x.AthleteId == athleteId)
            .Where(x => x.Recipient == "athlete")
            .Where(x => x.IsRead == false)
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

    public async Task<AppNotification?> GetByIdAsync(Guid notificationId)
    {
        var client = await _factory.GetClientAsync();
        var row = await client.From<NotificationRow>().Where(x => x.Id == notificationId).Single();
        return row is null ? null : Map(row);
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
