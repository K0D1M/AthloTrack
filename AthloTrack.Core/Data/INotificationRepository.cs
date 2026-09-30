using AthloTrack.Core.Models;

namespace AthloTrack.Core.Data;

public interface INotificationRepository
{
    /// <summary>Unread notifications for the athlete (polled on app open/refresh).</summary>
    Task<IReadOnlyList<AppNotification>> GetUnreadForAthleteAsync(Guid athleteId);
    /// <summary>Unread notifications for the signed-in coach (e.g. a workout was completed).</summary>
    Task<IReadOnlyList<AppNotification>> GetUnreadForCoachAsync();
    Task MarkReadAsync(Guid notificationId);
}
