using AthloTrack.Core.Models;
using AthloTrack.Core.Supabase;
using AthloTrack.Core.Supabase.Rows;
using Supabase.Postgrest;

namespace AthloTrack.Core.Data;

public sealed class AthleteRepository : IAthleteRepository
{
    private readonly SupabaseClientFactory _factory;

    public AthleteRepository(SupabaseClientFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<Athlete>> GetAllAsync()
    {
        var client = await _factory.GetClientAsync();
        var response = await client.From<AthleteRow>()
            .Order(x => x.FullName, Constants.Ordering.Ascending)
            .Get();
        return response.Models.Select(Map).ToList();
    }

    public async Task<Athlete?> GetByIdAsync(Guid id)
    {
        var client = await _factory.GetClientAsync();
        var row = await client.From<AthleteRow>()
            .Where(x => x.Id == id)
            .Single();
        return row is null ? null : Map(row);
    }

    public async Task<Athlete?> GetByAuthUserIdAsync(Guid authUserId)
    {
        var client = await _factory.GetClientAsync();
        var row = await client.From<AthleteRow>()
            .Where(x => x.AuthUserId == authUserId)
            .Single();
        return row is null ? null : Map(row);
    }

    public async Task<Athlete?> GetMostRecentAsync()
    {
        var client = await _factory.GetClientAsync();
        var response = await client.From<AthleteRow>()
            .Order(x => x.UpdatedAt, Constants.Ordering.Descending)
            .Limit(1)
            .Get();
        var row = response.Models.FirstOrDefault();
        return row is null ? null : Map(row);
    }

    public async Task<Athlete> AddAsync(Athlete athlete)
    {
        var client = await _factory.GetClientAsync();
        // A new athlete is recent activity too (unset, it was stored as 0001-01-01).
        athlete.UpdatedAt = DateTimeOffset.UtcNow;
        var response = await client.From<AthleteRow>().Insert(MapBack(athlete));
        return Map(response.Models.First());
    }

    public async Task<Athlete> UpdateAsync(Athlete athlete)
    {
        var client = await _factory.GetClientAsync();
        // An edit counts as recent activity for "Πρόσφατα".
        athlete.UpdatedAt = DateTimeOffset.UtcNow;
        var response = await client.From<AthleteRow>().Update(MapBack(athlete));
        return Map(response.Models.First());
    }

    public async Task DeleteAsync(Guid id)
    {
        var client = await _factory.GetClientAsync();
        await client.From<AthleteRow>().Where(x => x.Id == id).Delete();
    }

    private static Athlete Map(AthleteRow r) => new()
    {
        Id = r.Id,
        AuthUserId = r.AuthUserId,
        CoachId = r.CoachId,
        FullName = r.FullName,
        Email = r.Email,
        DateOfBirth = r.DateOfBirth is { } dob ? DateOnly.FromDateTime(dob) : null,
        HeightCm = r.HeightCm,
        ProfileImagePath = r.ProfileImagePath,
        Notes = r.Notes,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
    };

    private static AthleteRow MapBack(Athlete a) => new()
    {
        Id = a.Id,
        AuthUserId = a.AuthUserId,
        CoachId = a.CoachId,
        FullName = a.FullName,
        Email = string.IsNullOrWhiteSpace(a.Email) ? null : a.Email.Trim(),
        DateOfBirth = a.DateOfBirth?.ToDateTime(TimeOnly.MinValue),
        HeightCm = a.HeightCm,
        ProfileImagePath = a.ProfileImagePath,
        Notes = a.Notes,
        CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt,
    };
}
