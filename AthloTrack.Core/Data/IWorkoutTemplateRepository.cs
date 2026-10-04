using AthloTrack.Core.Models;

namespace AthloTrack.Core.Data;

public interface IWorkoutTemplateRepository
{
    /// <summary>The signed-in coach's templates, newest first (RLS-scoped).</summary>
    Task<IReadOnlyList<WorkoutTemplate>> GetAllAsync();
    Task<WorkoutTemplate> AddAsync(Guid coachId, string name, string content);
    Task DeleteAsync(Guid id);
}
