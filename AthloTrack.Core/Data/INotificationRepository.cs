using AthloTrack.Core.Models;

namespace AthloTrack.Core.Data;

public interface INotificationRepository
{
    /// <summary>Unread notifications for the athlete (polled on app open/refresh).</summary>
    Task<IReadOnlyList<AppNotification>> GetUnreadForAthleteAsync(Guid athleteId);
    Task MarkReadAsync(Guid notificationId);
}
