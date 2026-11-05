using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;
using AthloTrack.Core.Models;

namespace AthloTrack.Tests;

/// <summary>In-memory stand-ins for the Supabase repositories, recording what was called.</summary>
internal sealed class FakeWorkouts : IWorkoutRepository
{
    public List<WorkoutProgram> Items { get; } = new();
    public List<Guid> Completed { get; } = new();
    public List<Guid> Read { get; } = new();
    public List<Guid> Deleted { get; } = new();
    public List<(Guid Id, string Content, DateOnly Date, bool? CoachPresent)> Updated { get; } = new();
    public List<WorkoutProgram> Added { get; } = new();

    /// <summary>When set, loading waits for it: a load that is still running.</summary>
    public TaskCompletionSource? LoadGate { get; set; }

    public async Task<IReadOnlyList<WorkoutProgram>> GetAllAsync()
    {
        if (LoadGate is { } gate) await gate.Task;
        return Items.ToList();
    }

    public async Task<IReadOnlyList<WorkoutProgram>> GetForAthleteAsync(Guid athleteId)
    {
        if (LoadGate is { } gate) await gate.Task;
        return Items.Where(w => w.AthleteId == athleteId).ToList();
    }
    public Task<WorkoutProgram> AddAsync(WorkoutProgram program) { Added.Add(program); return Task.FromResult(program); }
    public Task MarkCompletedAsync(Guid id) { Completed.Add(id); return Task.CompletedTask; }

    public List<Guid> NotCompleted { get; } = new();
    public List<Guid> Reminded { get; } = new();

    /// <summary>The server refuses the next reminders with this reason.</summary>
    public ReminderRefusal? RemindRefusal { get; set; }

    public Task MarkNotCompletedAsync(Guid id)
    {
        NotCompleted.Add(id);
        if (Items.FirstOrDefault(w => w.Id == id) is { } w) w.NotCompletedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public List<(Guid Athlete, string Content, DateOnly Date, TimeOnly? Time, bool Completed, bool? Present)> AddedPast { get; } = new();

    public Task<Guid> AddPastAsync(Guid athleteId, string content, DateOnly date, TimeOnly? startTime, bool completed, bool? coachPresent)
    {
        AddedPast.Add((athleteId, content, date, startTime, completed, coachPresent));
        return Task.FromResult(Guid.NewGuid());
    }

    public List<Guid> Accepted { get; } = new();
    public List<(Guid Id, string? Reason)> Declined { get; } = new();
    public List<Guid> Cancelled { get; } = new();
    public List<Guid> Restored { get; } = new();

    /// <summary>The server refuses the next cancels with this reason.</summary>
    public CancelRefusal? CancelRefusal { get; set; }

    public Task AcceptAsync(Guid id)
    {
        Accepted.Add(id);
        if (Items.FirstOrDefault(w => w.Id == id) is { } w) w.AcceptedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task DeclineAsync(Guid id, string? reason)
    {
        Declined.Add((id, reason));
        if (Items.FirstOrDefault(w => w.Id == id) is { } w) { w.DeclinedAt = DateTimeOffset.UtcNow; w.DeclineReason = reason; }
        return Task.CompletedTask;
    }

    public Task CancelAsync(Guid id)
    {
        if (CancelRefusal is { } reason) throw new WorkoutCancelException(reason);
        Cancelled.Add(id);
        if (Items.FirstOrDefault(w => w.Id == id) is { } w) w.CancelledAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task RestoreAsync(Guid id)
    {
        Restored.Add(id);
        if (Items.FirstOrDefault(w => w.Id == id) is { } w) w.CancelledAt = null;
        return Task.CompletedTask;
    }

    public Task RemindAsync(Guid id)
    {
        if (RemindRefusal is { } reason) throw new WorkoutReminderException(reason);
        Reminded.Add(id);
        if (Items.FirstOrDefault(w => w.Id == id) is { } w) w.LastRemindedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<WorkoutProgram?> GetByIdAsync(Guid id) => Task.FromResult(Items.FirstOrDefault(w => w.Id == id));
    public List<TimeOnly?> UpdatedTimes { get; } = new();
    public Task UpdateAsync(Guid id, string content, DateOnly targetDate, bool? coachPresent, TimeOnly? startTime = null)
    {
        Updated.Add((id, content, targetDate, coachPresent));
        UpdatedTimes.Add(startTime);
        return Task.CompletedTask;
    }
    /// <summary>When set, MarkReadAsync waits for it: a save that is still in flight.</summary>
    public TaskCompletionSource? ReadGate { get; set; }

    public async Task MarkReadAsync(Guid id)
    {
        Read.Add(id);
        if (ReadGate is { } gate) await gate.Task;
    }
    public Task DeleteAsync(Guid id) { Deleted.Add(id); Items.RemoveAll(w => w.Id == id); return Task.CompletedTask; }
}

internal sealed class FakeTemplates : IWorkoutTemplateRepository
{
    public List<WorkoutTemplate> Items { get; } = new();

    public Task<IReadOnlyList<WorkoutTemplate>> GetAllAsync() => Task.FromResult<IReadOnlyList<WorkoutTemplate>>(Items.ToList());

    public Task<WorkoutTemplate> AddAsync(Guid coachId, string name, string content)
    {
        var template = new WorkoutTemplate { Id = Guid.NewGuid(), Name = name, Content = content };
        Items.Insert(0, template);
        return Task.FromResult(template);
    }

    public Task DeleteAsync(Guid id)
    {
        Items.RemoveAll(t => t.Id == id);
        return Task.CompletedTask;
    }
}

internal sealed class FakeAthletes : IAthleteRepository
{
    public List<Athlete> Items { get; } = new();

    public Task<IReadOnlyList<Athlete>> GetAllAsync() => Task.FromResult<IReadOnlyList<Athlete>>(Items.ToList());
    public Task<Athlete?> GetByIdAsync(Guid id) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id));
    public Task<Athlete?> GetByAuthUserIdAsync(Guid authUserId) => Task.FromResult(Items.FirstOrDefault(a => a.AuthUserId == authUserId));
    public Task<Athlete?> GetMostRecentAsync() => Task.FromResult(Items.FirstOrDefault());
    public Task<Athlete> AddAsync(Athlete athlete) { Items.Add(athlete); return Task.FromResult(athlete); }
    public Task<Athlete> UpdateAsync(Athlete athlete) => Task.FromResult(athlete);
    public Task DeleteAsync(Guid id) { Items.RemoveAll(a => a.Id == id); return Task.CompletedTask; }
}

internal sealed class FakeMeasurements : IMeasurementRepository
{
    public List<Measurement> Items { get; } = new();

    public Task<IReadOnlyList<Measurement>> GetForAthleteAsync(Guid athleteId) =>
        Task.FromResult<IReadOnlyList<Measurement>>(Items.Where(m => m.AthleteId == athleteId).ToList());
    public Task<Measurement> AddAsync(Measurement measurement) { Items.Add(measurement); return Task.FromResult(measurement); }
    public Task<Measurement> UpdateAsync(Measurement measurement) => Task.FromResult(measurement);
    public Task DeleteAsync(Guid id) { Items.RemoveAll(m => m.Id == id); return Task.CompletedTask; }
}

internal sealed class FakeAvatars : IAvatarService
{
    public Task<string> UploadAsync(Guid athleteId, byte[] image, string contentType) => Task.FromResult($"{athleteId}/photo.jpg");
    public Task<byte[]?> GetAsync(string? path) => Task.FromResult<byte[]?>(null);
    public Task RemoveAsync(string? path) => Task.CompletedTask;
}

internal static class Sessions
{
    public static SessionState Coach(Guid coachId) => new() { Role = UserRole.Coach, ProfileId = coachId, AuthUserId = Guid.NewGuid() };

    public static SessionState Athlete(Guid athleteId, Guid coachId) =>
        new() { Role = UserRole.Athlete, ProfileId = athleteId, CoachId = coachId, AuthUserId = Guid.NewGuid() };
}

internal sealed class FakeMeasurementFields : IMeasurementFieldRepository
{
    public List<MeasurementField> Items { get; } = new();

    public Task<IReadOnlyList<MeasurementField>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<MeasurementField>>(Items.OrderBy(f => f.Name).ToList());

    public Task<MeasurementField> AddAsync(Guid coachId, string name, string? unit)
    {
        if (Items.Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new DuplicateMeasurementFieldException();
        var field = new MeasurementField { Id = Guid.NewGuid(), CoachId = coachId, Name = name, Unit = unit };
        Items.Add(field);
        return Task.FromResult(field);
    }

    /// <summary>What UsageAsync answers per field (default 0).</summary>
    public Dictionary<Guid, int> Usage { get; } = new();
    public List<Guid> Deleted { get; } = new();

    public Task<MeasurementField> UpdateAsync(Guid id, string name, string? unit)
    {
        if (Items.Any(f => f.Id != id && string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new DuplicateMeasurementFieldException();
        var old = Items.Single(f => f.Id == id);
        var renamed = new MeasurementField { Id = id, CoachId = old.CoachId, Name = name, Unit = unit };
        Items[Items.IndexOf(old)] = renamed;
        return Task.FromResult(renamed);
    }

    public Task<int> UsageAsync(Guid id) => Task.FromResult(Usage.TryGetValue(id, out var n) ? n : 0);

    public Task<int> DeleteAsync(Guid id)
    {
        Deleted.Add(id);
        Items.RemoveAll(f => f.Id == id);
        return UsageAsync(id);
    }
}

internal sealed class FakeForms : IFormRepository
{
    public List<FormDefinition> Forms { get; } = new();
    public List<FormAssignment> Copies { get; } = new();
    public List<(Guid Copy, Dictionary<string, string> Answers)> Submitted { get; } = new();

    public Task<IReadOnlyList<FormDefinition>> GetFormsAsync() => Task.FromResult<IReadOnlyList<FormDefinition>>(Forms.ToList());

    public Task<FormDefinition?> GetFormAsync(Guid id) => Task.FromResult(Forms.FirstOrDefault(f => f.Id == id));

    public Task<FormDefinition> SaveFormAsync(FormDefinition form)
    {
        if (form.Id == Guid.Empty) form.Id = Guid.NewGuid();
        Forms.RemoveAll(f => f.Id == form.Id);
        Forms.Add(form);
        return Task.FromResult(form);
    }

    public Task DeleteFormAsync(Guid id)
    {
        Forms.RemoveAll(f => f.Id == id);
        Copies.RemoveAll(c => c.FormId == id);
        return Task.CompletedTask;
    }

    public Task<int> AssignAsync(Guid formId, IEnumerable<Guid> athleteIds)
    {
        var added = 0;
        foreach (var id in athleteIds.Distinct())
        {
            if (Copies.Any(c => c.FormId == formId && c.AthleteId == id)) continue;
            Copies.Add(new FormAssignment { Id = Guid.NewGuid(), FormId = formId, AthleteId = id, CreatedAt = DateTimeOffset.UtcNow });
            added++;
        }
        return Task.FromResult(added);
    }

    public Task<IReadOnlyList<FormAssignment>> GetAssignmentsAsync(Guid formId) =>
        Task.FromResult<IReadOnlyList<FormAssignment>>(Copies.Where(c => c.FormId == formId).ToList());

    public Task<IReadOnlyList<FormAssignment>> GetAssignmentsForAthleteAsync(Guid athleteId) =>
        Task.FromResult<IReadOnlyList<FormAssignment>>(Copies.Where(c => c.AthleteId == athleteId).ToList());

    public Task<IReadOnlyList<FormAssignment>> GetAllAssignmentsAsync() =>
        Task.FromResult<IReadOnlyList<FormAssignment>>(Copies.ToList());

    public Task<FormAssignment?> GetAssignmentAsync(Guid id) => Task.FromResult(Copies.FirstOrDefault(c => c.Id == id));

    public Task<DateTimeOffset> SubmitAsync(Guid assignmentId, IReadOnlyDictionary<string, string> answers)
    {
        var copy = Copies.First(c => c.Id == assignmentId);
        if (copy.IsSubmitted) throw new FormException("Το έντυπο έχει ήδη υποβληθεί.");
        copy.Answers = answers.ToDictionary(p => p.Key, p => p.Value);
        copy.SubmittedAt = DateTimeOffset.UtcNow;
        Submitted.Add((assignmentId, copy.Answers));
        return Task.FromResult(copy.SubmittedAt.Value);
    }
}
