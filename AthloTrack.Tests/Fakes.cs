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

    public Task<IReadOnlyList<WorkoutProgram>> GetAllAsync() => Task.FromResult<IReadOnlyList<WorkoutProgram>>(Items.ToList());
    public Task<IReadOnlyList<WorkoutProgram>> GetForAthleteAsync(Guid athleteId) =>
        Task.FromResult<IReadOnlyList<WorkoutProgram>>(Items.Where(w => w.AthleteId == athleteId).ToList());
    public Task<WorkoutProgram> AddAsync(WorkoutProgram program) { Added.Add(program); return Task.FromResult(program); }
    public Task MarkCompletedAsync(Guid id) { Completed.Add(id); return Task.CompletedTask; }
    public Task UpdateAsync(Guid id, string content, DateOnly targetDate, bool? coachPresent) { Updated.Add((id, content, targetDate, coachPresent)); return Task.CompletedTask; }
    /// <summary>When set, MarkReadAsync waits for it: a save that is still in flight.</summary>
    public TaskCompletionSource? ReadGate { get; set; }

    public async Task MarkReadAsync(Guid id)
    {
        Read.Add(id);
        if (ReadGate is { } gate) await gate.Task;
    }
    public Task DeleteAsync(Guid id) { Deleted.Add(id); Items.RemoveAll(w => w.Id == id); return Task.CompletedTask; }
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
