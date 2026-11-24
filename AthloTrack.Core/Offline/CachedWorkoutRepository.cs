using AthloTrack.Core.Data;
using AthloTrack.Core.Models;

namespace AthloTrack.Core.Offline;

/// <summary>
/// Workouts with an offline copy (Android). Online it passes everything through and keeps the
/// latest list. Without internet: the lists come from the copy, the athlete's and coach's answers
/// wait in a queue (shown at once, sent when the internet returns), and anything else says it
/// needs the internet.
/// </summary>
public sealed class CachedWorkoutRepository : IWorkoutRepository
{
    private readonly IWorkoutRepository _inner;
    private readonly OfflineCache _cache;
    private readonly OfflineState _state;
    private readonly SemaphoreSlim _flushLock = new(1, 1);

    public CachedWorkoutRepository(IWorkoutRepository inner, OfflineCache cache, OfflineState state)
    {
        _inner = inner;
        _cache = cache;
        _state = state;
        _state.SetPending(_cache.LoadPending().Count);
    }

    // ---- reads ----

    public async Task<IReadOnlyList<WorkoutProgram>> GetAllAsync()
    {
        try
        {
            await FlushAsync();
            var items = await _inner.GetAllAsync();
            _cache.SaveWorkouts(items);
            _state.SetOnline();
            return items;
        }
        catch (Exception ex) when (OfflineState.IsNetworkError(ex))
        {
            return FromCopy(ex, w => true);
        }
    }

    public async Task<IReadOnlyList<WorkoutProgram>> GetForAthleteAsync(Guid athleteId)
    {
        try
        {
            await FlushAsync();
            var items = await _inner.GetForAthleteAsync(athleteId);
            _state.SetOnline();
            return items;
        }
        catch (Exception ex) when (OfflineState.IsNetworkError(ex))
        {
            return FromCopy(ex, w => w.AthleteId == athleteId);
        }
    }

    public async Task<WorkoutProgram?> GetByIdAsync(Guid id)
    {
        try
        {
            return await _inner.GetByIdAsync(id);
        }
        catch (Exception ex) when (OfflineState.IsNetworkError(ex))
        {
            return FromCopy(ex, w => w.Id == id).FirstOrDefault();
        }
    }

    /// <summary>The saved list with the queued answers applied; no copy: «Χωρίς σύνδεση».</summary>
    private List<WorkoutProgram> FromCopy(Exception ex, Func<WorkoutProgram, bool> which)
    {
        var saved = _cache.LoadWorkouts();
        if (saved is null) throw new OfflineException(OfflineException.NoInternet, ex);
        _state.SetOffline(saved.SavedAt);
        var pending = _cache.LoadPending();
        foreach (var w in saved.Items)
        {
            foreach (var p in pending.Where(p => p.WorkoutId == w.Id)) Apply(w, p);
        }
        return saved.Items.Where(which).ToList();
    }

    // ---- answers: queued without internet ----

    public Task AcceptAsync(Guid id) => AnswerAsync(id, PendingKind.Accept, null, () => _inner.AcceptAsync(id));

    public Task DeclineAsync(Guid id, string? reason) =>
        AnswerAsync(id, PendingKind.Decline, reason, () => _inner.DeclineAsync(id, reason));

    public Task MarkCompletedAsync(Guid id) => AnswerAsync(id, PendingKind.Completed, null, () => _inner.MarkCompletedAsync(id));

    public Task MarkNotCompletedAsync(Guid id) =>
        AnswerAsync(id, PendingKind.NotCompleted, null, () => _inner.MarkNotCompletedAsync(id));

    public Task CoachAnswerAsync(Guid id, bool completed) =>
        AnswerAsync(id, completed ? PendingKind.CoachCompleted : PendingKind.CoachNotCompleted, null,
            () => _inner.CoachAnswerAsync(id, completed));

    private async Task AnswerAsync(Guid id, PendingKind kind, string? reason, Func<Task> send)
    {
        try
        {
            await send();
            _state.SetOnline();
        }
        catch (Exception ex) when (OfflineState.IsNetworkError(ex) && _cache.Enabled && _cache.LoadWorkouts() is not null)
        {
            var pending = _cache.LoadPending();
            pending.Add(new PendingAnswer(id, kind, reason, DateTimeOffset.UtcNow));
            _cache.SavePending(pending);
            _state.SetPending(pending.Count);
            _state.SetOffline(null);
        }
        catch (Exception ex) when (OfflineState.IsNetworkError(ex))
        {
            throw new OfflineException(OfflineException.NeedsInternet, ex);
        }
    }

    /// <summary>What the answer changes, so the list shows it before it's sent.</summary>
    internal static void Apply(WorkoutProgram w, PendingAnswer p)
    {
        w.IsPendingSync = true;
        switch (p.Kind)
        {
            case PendingKind.Accept:
                w.AcceptedAt ??= p.At;
                break;
            case PendingKind.Decline:
                w.DeclinedAt ??= p.At;
                w.DeclineReason ??= p.Reason;
                break;
            case PendingKind.Completed:
            case PendingKind.CoachCompleted:
                w.CompletedAt ??= p.At;
                w.AnsweredBy = p.Kind == PendingKind.CoachCompleted ? "coach" : "athlete";
                break;
            case PendingKind.NotCompleted:
            case PendingKind.CoachNotCompleted:
                w.NotCompletedAt ??= p.At;
                w.AnsweredBy = p.Kind == PendingKind.CoachNotCompleted ? "coach" : "athlete";
                break;
        }
    }

    /// <summary>
    /// Sends the queued answers, oldest first. Stops at the first network error (still offline);
    /// an answer the server refuses (the workout was cancelled, deleted or already answered) is
    /// dropped and the user is told.
    /// </summary>
    public async Task FlushAsync()
    {
        if (_cache.LoadPending().Count == 0) return;
        await _flushLock.WaitAsync();
        try
        {
            var pending = _cache.LoadPending();
            while (pending.Count > 0)
            {
                var p = pending[0];
                try
                {
                    await Send(p);
                }
                catch (Exception ex) when (OfflineState.IsNetworkError(ex))
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AthloTrack] queued answer refused: {ex.Message}");
                    var date = _cache.LoadWorkouts()?.Items.FirstOrDefault(w => w.Id == p.WorkoutId)?.TargetDate;
                    _state.Tell(date is { } d
                        ? $"Η απάντηση για το ασκησιολόγιο της {d:dd/MM} δεν στάλθηκε: {ex.Message}"
                        : $"Μια απάντηση δεν στάλθηκε: {ex.Message}");
                }
                pending.RemoveAt(0);
                _cache.SavePending(pending);
                _state.SetPending(pending.Count);
            }
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private Task Send(PendingAnswer p) => p.Kind switch
    {
        PendingKind.Accept => _inner.AcceptAsync(p.WorkoutId),
        PendingKind.Decline => _inner.DeclineAsync(p.WorkoutId, p.Reason),
        PendingKind.Completed => _inner.MarkCompletedAsync(p.WorkoutId),
        PendingKind.NotCompleted => _inner.MarkNotCompletedAsync(p.WorkoutId),
        PendingKind.CoachCompleted => _inner.CoachAnswerAsync(p.WorkoutId, true),
        _ => _inner.CoachAnswerAsync(p.WorkoutId, false),
    };

    // ---- read receipts: skipped offline, sent at the next online load ----

    public async Task MarkReadAsync(Guid id)
    {
        try
        {
            await _inner.MarkReadAsync(id);
        }
        catch (Exception ex) when (OfflineState.IsNetworkError(ex))
        {
        }
    }

    // ---- the rest needs the internet ----

    public Task<Guid> AddPastAsync(Guid athleteId, string content, DateOnly date, TimeOnly? startTime, bool completed, bool? coachPresent) =>
        Online(() => _inner.AddPastAsync(athleteId, content, date, startTime, completed, coachPresent));

    public Task<WorkoutProgram> AddAsync(WorkoutProgram program) => Online(() => _inner.AddAsync(program));
    public Task CancelAsync(Guid id) => Online(() => _inner.CancelAsync(id));
    public Task RestoreAsync(Guid id) => Online(() => _inner.RestoreAsync(id));
    public Task CoachUndoAnswerAsync(Guid id) => Online(() => _inner.CoachUndoAnswerAsync(id));
    public Task RemindAsync(Guid id) => Online(() => _inner.RemindAsync(id));

    public Task UpdateAsync(Guid id, string content, DateOnly targetDate, bool? coachPresent, TimeOnly? startTime = null) =>
        Online(() => _inner.UpdateAsync(id, content, targetDate, coachPresent, startTime));

    public Task DeleteAsync(Guid id) => Online(() => _inner.DeleteAsync(id));

    private static async Task Online(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (OfflineState.IsNetworkError(ex) && ex is not OfflineException)
        {
            throw new OfflineException(OfflineException.NeedsInternet, ex);
        }
    }

    private static async Task<T> Online<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (OfflineState.IsNetworkError(ex) && ex is not OfflineException)
        {
            throw new OfflineException(OfflineException.NeedsInternet, ex);
        }
    }
}
