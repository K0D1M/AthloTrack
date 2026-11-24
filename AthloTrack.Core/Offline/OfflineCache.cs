using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Models;

namespace AthloTrack.Core.Offline;

/// <summary>Who was signed in, for opening the app without internet.</summary>
public sealed record SessionSnapshot(UserRole Role, Guid ProfileId, string? DisplayName, string? ProfileImagePath, Guid? CoachId);

public enum PendingKind
{
    Accept,
    Decline,
    Completed,
    NotCompleted,
    CoachCompleted,
    CoachNotCompleted,
}

/// <summary>An answer given without internet, sent when it returns.</summary>
public sealed record PendingAnswer(Guid WorkoutId, PendingKind Kind, string? Reason, DateTimeOffset At);

/// <summary>A saved list and when it came from the server.</summary>
public sealed class Saved<T>
{
    public DateTimeOffset SavedAt { get; set; }
    public List<T> Items { get; set; } = [];
}

// Source-generated: reflection-based System.Text.Json is off in the trimmed Android build.
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, IgnoreReadOnlyProperties = true)]
[JsonSerializable(typeof(SessionSnapshot))]
[JsonSerializable(typeof(Saved<Athlete>))]
[JsonSerializable(typeof(Saved<WorkoutProgram>))]
[JsonSerializable(typeof(Saved<Measurement>))]
[JsonSerializable(typeof(Saved<MeasurementField>))]
[JsonSerializable(typeof(List<PendingAnswer>))]
internal partial class OfflineJsonContext : JsonSerializerContext
{
}

/// <summary>
/// The copies kept for offline use, per signed-in login (so another person signing in on the same
/// phone never sees them; sign-out removes them all).
/// </summary>
public sealed class OfflineCache
{
    private const string Root = "offline/";
    private readonly IOfflineStore _store;
    private readonly SessionState _session;

    public OfflineCache(IOfflineStore store, SessionState session)
    {
        _store = store;
        _session = session;
    }

    /// <summary>Nothing is kept on this head (desktop, browser).</summary>
    public bool Enabled => _store is not NullOfflineStore;

    private static string Key(Guid user, string name) => $"{Root}{user:N}/{name}";

    private Guid? User => _session.AuthUserId;

    // ---- session ----

    public void SaveSession(Guid authUserId, SessionSnapshot snapshot) =>
        Write(Key(authUserId, "session"), snapshot, OfflineJsonContext.Default.SessionSnapshot);

    public SessionSnapshot? LoadSession(Guid authUserId) =>
        Read(Key(authUserId, "session"), OfflineJsonContext.Default.SessionSnapshot);

    // ---- lists (for the signed-in login) ----

    public void SaveAthletes(IEnumerable<Athlete> items) => SaveList("athletes", items, OfflineJsonContext.Default.SavedAthlete);
    public Saved<Athlete>? LoadAthletes() => LoadList("athletes", OfflineJsonContext.Default.SavedAthlete);

    public void SaveWorkouts(IEnumerable<WorkoutProgram> items) => SaveList("workouts", items, OfflineJsonContext.Default.SavedWorkoutProgram);
    public Saved<WorkoutProgram>? LoadWorkouts() => LoadList("workouts", OfflineJsonContext.Default.SavedWorkoutProgram);

    public void SaveMeasurements(IEnumerable<Measurement> items) => SaveList("measurements", items, OfflineJsonContext.Default.SavedMeasurement);
    public Saved<Measurement>? LoadMeasurements() => LoadList("measurements", OfflineJsonContext.Default.SavedMeasurement);

    public void SaveFields(IEnumerable<MeasurementField> items) => SaveList("fields", items, OfflineJsonContext.Default.SavedMeasurementField);
    public Saved<MeasurementField>? LoadFields() => LoadList("fields", OfflineJsonContext.Default.SavedMeasurementField);

    // ---- answers waiting to be sent ----

    public List<PendingAnswer> LoadPending() =>
        User is { } u ? Read(Key(u, "pending"), OfflineJsonContext.Default.ListPendingAnswer) ?? [] : [];

    public void SavePending(List<PendingAnswer> items)
    {
        if (User is not { } u) return;
        if (items.Count == 0) _store.Delete(Key(u, "pending"));
        else Write(Key(u, "pending"), items, OfflineJsonContext.Default.ListPendingAnswer);
    }

    /// <summary>Sign-out: every copy on the device goes.</summary>
    public void ClearAll() => _store.DeleteAll(Root);

    // ---- helpers ----

    private void SaveList<T>(string name, IEnumerable<T> items, JsonTypeInfo<Saved<T>> info)
    {
        if (User is not { } u || !Enabled) return;
        Write(Key(u, name), new Saved<T> { SavedAt = DateTimeOffset.UtcNow, Items = items.ToList() }, info);
    }

    private Saved<T>? LoadList<T>(string name, JsonTypeInfo<Saved<T>> info) =>
        User is { } u ? Read(Key(u, name), info) : null;

    private void Write<T>(string key, T value, JsonTypeInfo<T> info)
    {
        if (!Enabled) return;
        try
        {
            _store.Write(key, JsonSerializer.Serialize(value, info));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AthloTrack] offline copy not saved ({key}): {ex.Message}");
        }
    }

    private T? Read<T>(string key, JsonTypeInfo<T> info)
    {
        try
        {
            return _store.Read(key) is { } text ? JsonSerializer.Deserialize(text, info) : default;
        }
        catch (Exception ex)
        {
            // An old or broken copy: as if there were none.
            Console.WriteLine($"[AthloTrack] offline copy unreadable ({key}): {ex.Message}");
            return default;
        }
    }
}
