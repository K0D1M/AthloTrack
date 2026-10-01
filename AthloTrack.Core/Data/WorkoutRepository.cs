using AthloTrack.Core.Models;
using AthloTrack.Core.Supabase;
using AthloTrack.Core.Supabase.Rows;
using Supabase.Postgrest;

namespace AthloTrack.Core.Data;

public sealed class WorkoutRepository : IWorkoutRepository
{
    private readonly SupabaseClientFactory _factory;

    public WorkoutRepository(SupabaseClientFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<WorkoutProgram>> GetAllAsync()
    {
        var client = await _factory.GetClientAsync();
        var response = await client.From<WorkoutProgramRow>()
            .Order(x => x.TargetDate, Constants.Ordering.Ascending)
            .Get();
        return response.Models.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<WorkoutProgram>> GetForAthleteAsync(Guid athleteId)
    {
        var client = await _factory.GetClientAsync();
        var response = await client.From<WorkoutProgramRow>()
            .Where(x => x.AthleteId == athleteId)
            .Order(x => x.TargetDate, Constants.Ordering.Ascending)
            .Get();
        return response.Models.Select(Map).ToList();
    }

    public async Task<WorkoutProgram> AddAsync(WorkoutProgram program)
    {
        var client = await _factory.GetClientAsync();
        var response = await client.From<WorkoutProgramRow>().Insert(MapBack(program));
        return Map(response.Models.First());
    }

    public async Task MarkCompletedAsync(Guid id)
    {
        var client = await _factory.GetClientAsync();
        // Only completed_at: the athlete's permissions (and a DB trigger) allow nothing else.
        await client.From<WorkoutProgramRow>()
            .Where(x => x.Id == id)
            .Set(x => x.CompletedAt!, DateTimeOffset.UtcNow)
            .Update();
    }

    public async Task UpdateAsync(Guid id, string content, DateOnly targetDate)
    {
        var client = await _factory.GetClientAsync();
        // Update the whole row model, like AddAsync. Set(x => x.TargetDate, date) serialized the
        // local midnight as UTC, so in Greece (UTC+3) the workout moved to the previous day.
        var row = await client.From<WorkoutProgramRow>().Where(x => x.Id == id).Single()
                  ?? throw new InvalidOperationException("Το ασκησιολόγιο δεν βρέθηκε.");
        row.Content = content;
        row.TargetDate = targetDate.ToDateTime(TimeOnly.MinValue);
        await row.Update<WorkoutProgramRow>();
    }

    public async Task MarkReadAsync(Guid id)
    {
        var client = await _factory.GetClientAsync();
        // Server-side: only the caller's own workout, and only the first time.
        await client.Rpc("mark_workout_read", new Dictionary<string, object> { ["p_workout"] = id });
    }

    public async Task DeleteAsync(Guid id)
    {
        var client = await _factory.GetClientAsync();
        await client.From<WorkoutProgramRow>().Where(x => x.Id == id).Delete();
    }

    private static WorkoutProgram Map(WorkoutProgramRow r) => new()
    {
        Id = r.Id,
        AthleteId = r.AthleteId,
        Title = r.Title,
        Content = r.Content,
        TargetDate = DateOnly.FromDateTime(r.TargetDate),
        CreatedBy = r.CreatedBy,
        CreatedAt = r.CreatedAt,
        CompletedAt = r.CompletedAt,
        ReadAt = r.ReadAt,
    };

    private static WorkoutProgramRow MapBack(WorkoutProgram w) => new()
    {
        Id = w.Id,
        AthleteId = w.AthleteId,
        Title = w.Title,
        Content = w.Content,
        TargetDate = w.TargetDate.ToDateTime(TimeOnly.MinValue),
        CreatedBy = w.CreatedBy,
        CreatedAt = w.CreatedAt,
    };
}
