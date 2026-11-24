using AthloTrack.Core.Data;
using AthloTrack.Core.Models;

namespace AthloTrack.Core.Offline;

/// <summary>The lists Προπονήσεις and Ημερολόγιο need, read from the offline copy without internet.</summary>
internal static class OfflineRead
{
    public static async Task<IReadOnlyList<T>> ListAsync<T>(Func<Task<IReadOnlyList<T>>> load,
        Action<IReadOnlyList<T>> save, Func<Saved<T>?> copy, OfflineState state)
    {
        try
        {
            var items = await load();
            save(items);
            state.SetOnline();
            return items;
        }
        catch (Exception ex) when (OfflineState.IsNetworkError(ex))
        {
            if (copy() is not { } saved) throw new OfflineException(OfflineException.NoInternet, ex);
            state.SetOffline(saved.SavedAt);
            return saved.Items;
        }
    }
}

/// <summary>Athletes with an offline copy of the list (names in Προπονήσεις and Ημερολόγιο).</summary>
public sealed class CachedAthleteRepository(IAthleteRepository inner, OfflineCache cache, OfflineState state) : IAthleteRepository
{
    public Task<IReadOnlyList<Athlete>> GetAllAsync() =>
        OfflineRead.ListAsync(inner.GetAllAsync, cache.SaveAthletes, cache.LoadAthletes, state);

    public async Task<Athlete?> GetByIdAsync(Guid id)
    {
        try
        {
            return await inner.GetByIdAsync(id);
        }
        catch (Exception ex) when (OfflineState.IsNetworkError(ex) && cache.LoadAthletes() is { } saved)
        {
            state.SetOffline(saved.SavedAt);
            return saved.Items.FirstOrDefault(a => a.Id == id);
        }
    }

    public Task<Athlete?> GetByAuthUserIdAsync(Guid authUserId) => inner.GetByAuthUserIdAsync(authUserId);
    public Task<Athlete?> GetMostRecentAsync() => inner.GetMostRecentAsync();
    public Task<Athlete> AddAsync(Athlete athlete) => inner.AddAsync(athlete);
    public Task<Athlete> UpdateAsync(Athlete athlete) => inner.UpdateAsync(athlete);
    public Task DeleteAsync(Guid id) => inner.DeleteAsync(id);
}

/// <summary>Measurements with an offline copy (Ημερολόγιο).</summary>
public sealed class CachedMeasurementRepository(IMeasurementRepository inner, OfflineCache cache, OfflineState state) : IMeasurementRepository
{
    public Task<IReadOnlyList<Measurement>> GetAllAsync() =>
        OfflineRead.ListAsync(inner.GetAllAsync, cache.SaveMeasurements, cache.LoadMeasurements, state);

    public async Task<IReadOnlyList<Measurement>> GetForAthleteAsync(Guid athleteId)
    {
        try
        {
            return await inner.GetForAthleteAsync(athleteId);
        }
        catch (Exception ex) when (OfflineState.IsNetworkError(ex) && cache.LoadMeasurements() is { } saved)
        {
            state.SetOffline(saved.SavedAt);
            return saved.Items.Where(m => m.AthleteId == athleteId).ToList();
        }
    }

    public Task<Measurement> AddAsync(Measurement measurement) => inner.AddAsync(measurement);
    public Task<Measurement> UpdateAsync(Measurement measurement) => inner.UpdateAsync(measurement);
    public Task DeleteAsync(Guid id) => inner.DeleteAsync(id);
}

/// <summary>Measurement fields with an offline copy (the values' names in Ημερολόγιο).</summary>
public sealed class CachedMeasurementFieldRepository(IMeasurementFieldRepository inner, OfflineCache cache, OfflineState state) : IMeasurementFieldRepository
{
    public Task<IReadOnlyList<MeasurementField>> GetAllAsync() =>
        OfflineRead.ListAsync(inner.GetAllAsync, cache.SaveFields, cache.LoadFields, state);

    public Task<MeasurementField> AddAsync(Guid coachId, string name, string? unit) => inner.AddAsync(coachId, name, unit);
    public Task<MeasurementField> UpdateAsync(Guid id, string name, string? unit) => inner.UpdateAsync(id, name, unit);
    public Task<int> UsageAsync(Guid id) => inner.UsageAsync(id);
    public Task<int> DeleteAsync(Guid id) => inner.DeleteAsync(id);
}
