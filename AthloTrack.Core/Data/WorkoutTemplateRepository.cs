using AthloTrack.Core.Models;
using AthloTrack.Core.Supabase;
using AthloTrack.Core.Supabase.Rows;
using Supabase.Postgrest;

namespace AthloTrack.Core.Data;

public sealed class WorkoutTemplateRepository : IWorkoutTemplateRepository
{
    private readonly SupabaseClientFactory _factory;

    public WorkoutTemplateRepository(SupabaseClientFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<WorkoutTemplate>> GetAllAsync()
    {
        var client = await _factory.GetClientAsync();
        var response = await client.From<WorkoutTemplateRow>()
            .Order(x => x.CreatedAt, Constants.Ordering.Descending)
            .Get();
        return response.Models.Select(Map).ToList();
    }

    public async Task<WorkoutTemplate> AddAsync(Guid coachId, string name, string content)
    {
        var client = await _factory.GetClientAsync();
        var response = await client.From<WorkoutTemplateRow>()
            .Insert(new WorkoutTemplateRow { CoachId = coachId, Name = name, Content = content });
        return Map(response.Models.First());
    }

    public async Task DeleteAsync(Guid id)
    {
        var client = await _factory.GetClientAsync();
        await client.From<WorkoutTemplateRow>().Where(x => x.Id == id).Delete();
    }

    private static WorkoutTemplate Map(WorkoutTemplateRow r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        Content = r.Content,
        CreatedAt = r.CreatedAt,
    };
}
