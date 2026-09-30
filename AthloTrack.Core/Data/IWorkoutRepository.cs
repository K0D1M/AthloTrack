using AthloTrack.Core.Models;

namespace AthloTrack.Core.Data;

public interface IWorkoutRepository
{
    /// <summary>All workout programs the current user can see (RLS-scoped).</summary>
    Task<IReadOnlyList<WorkoutProgram>> GetAllAsync();
    Task<IReadOnlyList<WorkoutProgram>> GetForAthleteAsync(Guid athleteId);
    Task<WorkoutProgram> AddAsync(WorkoutProgram program);
    /// <summary>The athlete marks the program done; the DB then notifies the coach.</summary>
    Task MarkCompletedAsync(Guid id);
    Task DeleteAsync(Guid id);
}
