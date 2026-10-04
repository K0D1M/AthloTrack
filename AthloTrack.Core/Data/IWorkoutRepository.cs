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
    /// <summary>
    /// The coach changes the program, its date or their presence; the DB clears the read receipt
    /// and notifies the athlete.
    /// </summary>
    Task UpdateAsync(Guid id, string content, DateOnly targetDate, bool? coachPresent);
    /// <summary>The athlete has seen this workout on screen: stamps read_at once (coach's read receipt).</summary>
    Task MarkReadAsync(Guid id);
    Task DeleteAsync(Guid id);
}
