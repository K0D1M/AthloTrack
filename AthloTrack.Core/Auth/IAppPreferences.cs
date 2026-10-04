namespace AthloTrack.Core.Auth;

/// <summary>
/// Small per-device settings that survive logout and restarts (e.g. the role last used on the
/// login screen). Each head stores them its own way; nothing secret goes here.
/// </summary>
public interface IAppPreferences
{
    string? Get(string key);
    void Set(string key, string? value);
}

/// <summary>Fallback when a head registers no store: remembered for this run only.</summary>
public sealed class InMemoryAppPreferences : IAppPreferences
{
    private readonly Dictionary<string, string> _values = new();

    public string? Get(string key) => _values.TryGetValue(key, out var value) ? value : null;

    public void Set(string key, string? value)
    {
        if (value is null) _values.Remove(key);
        else _values[key] = value;
    }
}
