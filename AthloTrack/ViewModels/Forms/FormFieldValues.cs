using System;
using System.Collections.Generic;
using System.Linq;
using AthloTrack.Core.Forms;

namespace AthloTrack.ViewModels;

/// <summary>
/// The athlete's answers to a form's fields ("f1", "f2", …), shared by the fill-in page and its
/// document view: every change is saved as a draft; required fields left empty are marked.
/// </summary>
public sealed class FormFieldValues
{
    private readonly Dictionary<string, string> _values = new();
    private readonly HashSet<string> _missing = new();

    /// <summary>Raised on every change (the device draft, the «3 από 9» count).</summary>
    public event Action? Changed;

    /// <summary>Raised when the fields marked as missing change.</summary>
    public event Action? MissingChanged;

    public string? Get(string key) => _values.GetValueOrDefault(key);

    public void Set(string key, string? value)
    {
        if (string.IsNullOrEmpty(value)) _values.Remove(key);
        else _values[key] = value;
        if (!string.IsNullOrWhiteSpace(value) && _missing.Remove(key)) MissingChanged?.Invoke();
        Changed?.Invoke();
    }

    public bool IsMissing(string key) => _missing.Contains(key);

    /// <summary>Marks these fields red until filled.</summary>
    public void SetMissing(IEnumerable<string> keys)
    {
        _missing.Clear();
        foreach (var k in keys) _missing.Add(k);
        MissingChanged?.Invoke();
    }

    /// <summary>The field answers among a copy's answers (drafts, or the submitted copy).</summary>
    public void Load(IReadOnlyDictionary<string, string> answers)
    {
        _values.Clear();
        _missing.Clear();
        foreach (var (k, v) in answers)
            if (k.Length > 1 && k[0] == 'f' && k[1..].All(char.IsDigit)) _values[k] = v;
    }

    /// <summary>The filled answers, trimmed (tick boxes only when ticked).</summary>
    public IReadOnlyDictionary<string, string> Answers() =>
        _values.Where(kv => !string.IsNullOrWhiteSpace(kv.Value)).ToDictionary(kv => kv.Key, kv => kv.Value.Trim());

    /// <summary>How many of these fields are filled.</summary>
    public int FilledCount(IEnumerable<FormField> fields) =>
        fields.Count(f => !string.IsNullOrWhiteSpace(Get(f.Key)));
}
