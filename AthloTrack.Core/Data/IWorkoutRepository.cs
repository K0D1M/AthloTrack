using AthloTrack.Core.Models;

namespace AthloTrack.Core.Data;

/// <summary>Why the server refused a reminder.</summary>
public enum ReminderRefusal
{
    TooSoon,
    AlreadyAnswered,
    NotAllowed,
    NotInstalled,
}

public sealed class WorkoutReminderException : Exception
{
    public WorkoutReminderException(ReminderRefusal reason, Exception? inner = null) : base(reason.ToString(), inner)
    {
        Reason = reason;
    }

    public ReminderRefusal Reason { get; }
}

/// <summary>Why the server refused to cancel or restore a workout.</summary>
public enum CancelRefusal
{
    AlreadyAnswered,
    NotAllowed,
    NotInstalled,
}

public sealed class WorkoutCancelException : Exception
{
    public WorkoutCancelException(CancelRefusal reason, Exception? inner = null) : base(reason.ToString(), inner)
    {
        Reason = reason;
    }

    public CancelRefusal Reason { get; }
}

/// <summary>The server refused an old workout (not before today, not the coach's athlete, 017 not run).</summary>
public sealed class PastWorkoutException(string message, Exception? inner = null) : Exception(message, inner);

public interface IWorkoutRepository
{
    /// <summary>
    /// A workout from before the app, already answered («Ολοκληρώθηκε» / «Δεν ολοκληρώθηκε»), with no
    /// notification to the athlete. Only for a date before today. Throws <see cref="PastWorkoutException"/>.
    /// </summary>
    Task<Guid> AddPastAsync(Guid athleteId, string content, DateOnly date, TimeOnly? startTime, bool completed, bool? coachPresent);

    /// <summary>All workout programs the current user can see (RLS-scoped).</summary>
    Task<IReadOnlyList<WorkoutProgram>> GetAllAsync();
    Task<IReadOnlyList<WorkoutProgram>> GetForAthleteAsync(Guid athleteId);
    Task<WorkoutProgram> AddAsync(WorkoutProgram program);
    /// <summary>The athlete's «Θα το κάνω» (final); the DB then notifies the coach.</summary>
    Task AcceptAsync(Guid id);
    /// <summary>The athlete's «Δεν θα το κάνω» with an optional reason (final); the DB notifies the coach.</summary>
    Task DeclineAsync(Guid id, string? reason);
    /// <summary>
    /// The coach cancels it (the athlete is notified). Throws <see cref="WorkoutCancelException"/>
    /// when the server refuses (already answered or declined, not theirs, 013 not run).
    /// </summary>
    Task CancelAsync(Guid id);
    /// <summary>The coach undoes a cancel (the athlete is notified).</summary>
    Task RestoreAsync(Guid id);
    /// <summary>The athlete marks the program done; the DB then notifies the coach.</summary>
    Task MarkCompletedAsync(Guid id);
    /// <summary>The athlete answers «Δεν ολοκληρώθηκε» (final); the DB then notifies the coach.</summary>
    Task MarkNotCompletedAsync(Guid id);
    /// <summary>
    /// The coach asks the athlete whether it was done: only for an unanswered workout, at most
    /// once an hour. Throws <see cref="WorkoutReminderException"/> when the server refuses.
    /// </summary>
    Task RemindAsync(Guid id);
    /// <summary>One workout (RLS-scoped), or null.</summary>
    Task<WorkoutProgram?> GetByIdAsync(Guid id);
    /// <summary>
    /// The coach changes the program, its date and time or their presence; the DB clears the read
    /// receipt and notifies the athlete.
    /// </summary>
    Task UpdateAsync(Guid id, string content, DateOnly targetDate, bool? coachPresent, TimeOnly? startTime = null);
    /// <summary>The athlete has seen this workout on screen: stamps read_at once (coach's read receipt).</summary>
    Task MarkReadAsync(Guid id);
    Task DeleteAsync(Guid id);
}
