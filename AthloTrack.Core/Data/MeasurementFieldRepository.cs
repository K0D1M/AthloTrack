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

    /// <summary>Renames a field (name and unit). Throws <see cref="DuplicateMeasurementFieldException"/> for a name in use.</summary>
    Task<MeasurementField> UpdateAsync(Guid id, string name, string? unit);

    /// <summary>How many measurements have a value for the field.</summary>
    Task<int> UsageAsync(Guid id);

    /// <summary>Deletes the field and its values from every measurement; returns how many values went.</summary>
    Task<int> DeleteAsync(Guid id);
}

/// <summary>The server refused managing a field (not the caller's, or 016 not run yet).</summary>
public sealed class MeasurementFieldManageException(string message, Exception? inner = null) : Exception(message, inner);

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

    public async Task<MeasurementField> UpdateAsync(Guid id, string name, string? unit)
    {
        var client = await _factory.GetClientAsync();
        try
        {
            var response = await client.From<MeasurementFieldRow>()
                .Where(x => x.Id == id)
                .Set(x => x.Name, name.Trim())
                .Set(x => x.Unit!, string.IsNullOrWhiteSpace(unit) ? null! : unit.Trim())
                .Update();
            return Map(response.Models.First());
        }
        catch (global::Supabase.Postgrest.Exceptions.PostgrestException ex)
            when (ex.Message.Contains("23505") || ex.Message.Contains("uq_measurement_fields_coach_name"))
        {
            throw new DuplicateMeasurementFieldException(ex);
        }
    }

    public Task<int> UsageAsync(Guid id) => CallAsync("measurement_field_usage", id);

    public Task<int> DeleteAsync(Guid id) => CallAsync("delete_measurement_field", id);

    private async Task<int> CallAsync(string function, Guid id)
    {
        var client = await _factory.GetClientAsync();
        try
        {
            var response = await client.Rpc(function, new Dictionary<string, object> { ["p_field"] = id });
            return int.TryParse(response.Content?.Trim(), out var n) ? n : 0;
        }
        catch (global::Supabase.Postgrest.Exceptions.PostgrestException ex)
        {
            if (ex.Message.Contains("PGRST202"))
                throw new MeasurementFieldManageException("Η διαχείριση πεδίων δεν είναι ακόμα διαθέσιμη (λείπει το 016_measurements_more.sql).", ex);
            if (ex.Message.Contains("not allowed"))
                throw new MeasurementFieldManageException("Μόνο ο προπονητής που έφτιαξε το πεδίο μπορεί να το αλλάξει.", ex);
            throw;
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
