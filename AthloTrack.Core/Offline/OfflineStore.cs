namespace AthloTrack.Core.Offline;

/// <summary>
/// Where the offline copies live (text by key). The Android app keeps them in its private files;
/// the other heads use <see cref="NullOfflineStore"/> and keep nothing.
/// </summary>
public interface IOfflineStore
{
    string? Read(string key);
    void Write(string key, string text);
    void Delete(string key);

    /// <summary>Every key that starts with <paramref name="prefix"/> (sign-out).</summary>
    void DeleteAll(string prefix);
}

/// <summary>No offline copies (Desktop, browser).</summary>
public sealed class NullOfflineStore : IOfflineStore
{
    public string? Read(string key) => null;
    public void Write(string key, string text) { }
    public void Delete(string key) { }
    public void DeleteAll(string prefix) { }
}

/// <summary>For tests: copies kept in memory.</summary>
public sealed class MemoryOfflineStore : IOfflineStore
{
    public Dictionary<string, string> Items { get; } = new();
    public string? Read(string key) => Items.TryGetValue(key, out var v) ? v : null;
    public void Write(string key, string text) => Items[key] = text;
    public void Delete(string key) => Items.Remove(key);

    public void DeleteAll(string prefix)
    {
        foreach (var key in Items.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            Items.Remove(key);
    }
}
