using AthloTrack.Core.Models;

namespace AthloTrack.Core.Data;

public interface IMeasurementRepository
{
    Task<IReadOnlyList<Measurement>> GetForAthleteAsync(Guid athleteId);
    Task<Measurement> AddAsync(Measurement measurement);
    Task<Measurement> UpdateAsync(Measurement measurement);
    Task DeleteAsync(Guid id);
}
