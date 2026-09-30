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
