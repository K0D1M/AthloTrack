using AthloTrack.Core.Models;
using AthloTrack.Core.Supabase;
using AthloTrack.Core.Supabase.Rows;

namespace AthloTrack.Core.Data;

public sealed class CoachRepository : ICoachRepository
{
    private readonly SupabaseClientFactory _factory;

    public CoachRepository(SupabaseClientFactory factory)
    {
        _factory = factory;
    }

    public async Task<Coach?> GetByAuthUserIdAsync(Guid authUserId)
    {
        var client = await _factory.GetClientAsync();
        var row = await client.From<CoachRow>()
            .Where(x => x.AuthUserId == authUserId)
            .Single();
        return row is null ? null : Map(row);
    }

    public async Task<Coach?> GetByIdAsync(Guid id)
    {
        var client = await _factory.GetClientAsync();
        var row = await client.From<CoachRow>()
            .Where(x => x.Id == id)
            .Single();
        return row is null ? null : Map(row);
    }

    public async Task SetProfileImagePathAsync(Guid coachId, string? path)
    {
        var client = await _factory.GetClientAsync();
        await client.From<CoachRow>()
            .Where(x => x.Id == coachId)
            .Set(x => x.ProfileImagePath!, path)
            .Update();
    }

    private static Coach Map(CoachRow row) => new()
    {
        Id = row.Id,
        AuthUserId = row.AuthUserId,
        FullName = row.FullName,
        Email = row.Email,
        ProfileImagePath = row.ProfileImagePath,
        CreatedAt = row.CreatedAt,
    };
}
