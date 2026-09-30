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
    /// <summary>The athlete has seen their workouts: stamps read_at on the unread ones (coach's read receipt).</summary>
    Task MarkAllReadAsync();
    Task DeleteAsync(Guid id);
}
