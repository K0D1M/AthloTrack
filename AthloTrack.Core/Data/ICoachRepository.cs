using AthloTrack.Core.Models;

namespace AthloTrack.Core.Data;

public interface ICoachRepository
{
    Task<Coach?> GetByAuthUserIdAsync(Guid authUserId);

    /// <summary>A coach by id — athletes can read their own coach (RLS).</summary>
    Task<Coach?> GetByIdAsync(Guid id);

    /// <summary>Sets only coaches.profile_image_path.</summary>
    Task SetProfileImagePathAsync(Guid coachId, string? path);
}
