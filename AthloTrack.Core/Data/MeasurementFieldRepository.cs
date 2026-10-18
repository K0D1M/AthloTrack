using AthloTrack.Core.Models;
using AthloTrack.Core.Supabase;
using AthloTrack.Core.Supabase.Rows;
using Supabase.Postgrest;

namespace AthloTrack.Core.Data;

/// <summary>The name is already one of this coach's fields (case and spaces ignored).</summary>
public sealed class DuplicateMeasurementFieldException(Exception? inner = null)
    : Exception("Υπάρχει ήδη πεδίο με αυτό το όνομα.", inner);

public interface IMeasurementFieldRepository
{
    /// <summary>The coach's own fields, or (for an athlete) their coach's; by name.</summary>
    Task<IReadOnlyList<MeasurementField>> GetAllAsync();

    /// <summary>Saves a new field. Throws <see cref="DuplicateMeasurementFieldException"/> for a name in use.</summary>
    Task<MeasurementField> AddAsync(Guid coachId, string name, string? unit);
}

public sealed class MeasurementFieldRepository : IMeasurementFieldRepository
{
    private readonly SupabaseClientFactory _factory;

    public MeasurementFieldRepository(SupabaseClientFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<MeasurementField>> GetAllAsync()
    {
        var client = await _factory.GetClientAsync();
        var response = await client.From<MeasurementFieldRow>()
            .Order(x => x.Name, Constants.Ordering.Ascending)
            .Get();
        return response.Models.Select(Map).ToList();
    }

    public async Task<MeasurementField> AddAsync(Guid coachId, string name, string? unit)
    {
        var client = await _factory.GetClientAsync();
        try
        {
            var response = await client.From<MeasurementFieldRow>().Insert(new MeasurementFieldRow
            {
                Id = Guid.NewGuid(),
                CoachId = coachId,
                Name = name.Trim(),
                Unit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim(),
            });
            return Map(response.Models.First());
        }
        catch (global::Supabase.Postgrest.Exceptions.PostgrestException ex)
            when (ex.Message.Contains("23505") || ex.Message.Contains("uq_measurement_fields_coach_name"))
        {
            throw new DuplicateMeasurementFieldException(ex);
        }
    }

    private static MeasurementField Map(MeasurementFieldRow r) => new()
    {
        Id = r.Id,
        CoachId = r.CoachId,
        Name = r.Name,
        Unit = r.Unit,
    };
}
