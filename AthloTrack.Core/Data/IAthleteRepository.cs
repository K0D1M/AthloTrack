using AthloTrack.Core.Models;

namespace AthloTrack.Core.Data;

public interface IAthleteRepository
{
    /// <summary>Athletes visible to the current user (RLS scopes this to the coach's own athletes).</summary>
    Task<IReadOnlyList<Athlete>> GetAllAsync();

    Task<Athlete?> GetByIdAsync(Guid id);

    Task<Athlete?> GetByAuthUserIdAsync(Guid authUserId);

    /// <summary>The most recently updated athlete — drives the "Πρόσφατα" home screen.</summary>
    Task<Athlete?> GetMostRecentAsync();

    Task<Athlete> AddAsync(Athlete athlete);

    Task<Athlete> UpdateAsync(Athlete athlete);

    Task DeleteAsync(Guid id);
}
