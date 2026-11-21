using AthloTrack.Core.Models;

namespace AthloTrack.Core.Data;

public interface IMeasurementRepository
{
    Task<IReadOnlyList<Measurement>> GetForAthleteAsync(Guid athleteId);

    /// <summary>Every measurement the user may read (Ημερολόγιο): oldest first, then by creation.</summary>
    Task<IReadOnlyList<Measurement>> GetAllAsync();
    Task<Measurement> AddAsync(Measurement measurement);
    Task<Measurement> UpdateAsync(Measurement measurement);
    Task DeleteAsync(Guid id);
}
