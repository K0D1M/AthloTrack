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
        Extra = ExtraFromJson(r.Extra),
        Notes = r.Notes,
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
        Extra = ExtraToJson(m.Extra),
        Notes = string.IsNullOrWhiteSpace(m.Notes) ? null : m.Notes.Trim(),
        CreatedBy = m.CreatedBy,
        CreatedAt = m.CreatedAt,
    };

    /// <summary>Field id → number; anything else in the JSON is skipped.</summary>
    public static Dictionary<Guid, decimal> ExtraFromJson(Newtonsoft.Json.Linq.JObject? json)
    {
        var values = new Dictionary<Guid, decimal>();
        if (json is null) return values;
        foreach (var (key, token) in json)
        {
            if (!Guid.TryParse(key, out var id) || token is null) continue;
            if (token.Type is Newtonsoft.Json.Linq.JTokenType.Integer or Newtonsoft.Json.Linq.JTokenType.Float)
                values[id] = token.ToObject<decimal>();
        }
        return values;
    }

    public static Newtonsoft.Json.Linq.JObject? ExtraToJson(Dictionary<Guid, decimal>? values)
    {
        if (values is null || values.Count == 0) return null;
        var json = new Newtonsoft.Json.Linq.JObject();
        foreach (var (id, value) in values) json[id.ToString()] = value;
        return json;
    }
}
