using AthloTrack.Core.Models;

namespace AthloTrack.Core.Data;

/// <summary>«Έντυπα» (018): the coach's forms and each athlete's copy.</summary>
public interface IFormRepository
{
    /// <summary>The coach's forms (an athlete gets only the forms sent to them).</summary>
    Task<IReadOnlyList<FormDefinition>> GetFormsAsync();

    Task<FormDefinition?> GetFormAsync(Guid id);

    /// <summary>Adds a new form (Id empty) or saves an existing one; returns it as stored.</summary>
    Task<FormDefinition> SaveFormAsync(FormDefinition form);

    /// <summary>Deletes the form and every athlete's copy with their answers.</summary>
    Task DeleteFormAsync(Guid id);

    /// <summary>Sends the form to these athletes; ones that already have it are skipped. Returns how many got it.</summary>
    Task<int> AssignAsync(Guid formId, IEnumerable<Guid> athleteIds);

    /// <summary>Every copy of a form (coach).</summary>
    Task<IReadOnlyList<FormAssignment>> GetAssignmentsAsync(Guid formId);

    /// <summary>One athlete's copies, newest first.</summary>
    Task<IReadOnlyList<FormAssignment>> GetAssignmentsForAthleteAsync(Guid athleteId);

    /// <summary>All copies the signed-in user can see (coach: of their forms; athlete: their own).</summary>
    Task<IReadOnlyList<FormAssignment>> GetAllAssignmentsAsync();

    Task<FormAssignment?> GetAssignmentAsync(Guid id);

    /// <summary>The athlete's final answers; returns when it was submitted.</summary>
    Task<DateTimeOffset> SubmitAsync(Guid assignmentId, IReadOnlyDictionary<string, string> answers);
}

/// <summary>A form change the database refused, with the reason in Greek.</summary>
public sealed class FormException(string message, Exception? inner = null) : Exception(message, inner);
