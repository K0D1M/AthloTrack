using AthloTrack.Core.Models;
using AthloTrack.Core.Supabase;
using AthloTrack.Core.Supabase.Rows;
using Supabase.Postgrest;

namespace AthloTrack.Core.Data;

public sealed class MeasurementRepository : IMeasurementRepository
{
    private readonly SupabaseClientFactory _factory;

    public MeasurementRepository(SupabaseClientFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<Measurement>> GetForAthleteAsync(Guid athleteId)
    {
        var client = await _factory.GetClientAsync();
        var response = await client.From<MeasurementRow>()
            .Where(x => x.AthleteId == athleteId)
            .Order(x => x.MeasuredAt, Constants.Ordering.Ascending)
            .Get();
        return response.Models.Select(Map).ToList();
    }

    public async Task<Measurement> AddAsync(Measurement measurement)
    {
        var client = await _factory.GetClientAsync();
        var response = await client.From<MeasurementRow>().Insert(MapBack(measurement));
        return Map(response.Models.First());
    }

    public async Task<Measurement> UpdateAsync(Measurement measurement)
    {
        var client = await _factory.GetClientAsync();
        var response = await client.From<MeasurementRow>().Update(MapBack(measurement));
        return Map(response.Models.First());
    }

    public async Task DeleteAsync(Guid id)
    {
        var client = await _factory.GetClientAsync();
        await client.From<MeasurementRow>().Where(x => x.Id == id).Delete();
    }

    private static Measurement Map(MeasurementRow r) => new()
    {
        Id = r.Id,
        AthleteId = r.AthleteId,
        MeasuredAt = DateOnly.FromDateTime(r.MeasuredAt),
        WeightKg = r.WeightKg,
        FatMassWt = r.FatMassWt,
        FatHgt = r.FatHgt,
        CreatedBy = r.CreatedBy,
        CreatedAt = r.CreatedAt,
    };

    private static MeasurementRow MapBack(Measurement m) => new()
    {
        Id = m.Id,
        AthleteId = m.AthleteId,
        MeasuredAt = m.MeasuredAt.ToDateTime(TimeOnly.MinValue),
        WeightKg = m.WeightKg,
        FatMassWt = m.FatMassWt,
        FatHgt = m.FatHgt,
        CreatedBy = m.CreatedBy,
        CreatedAt = m.CreatedAt,
    };
}
