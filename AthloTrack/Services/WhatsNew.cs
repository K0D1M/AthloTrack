using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AthloTrack.Core.Auth;

namespace AthloTrack.Services;

/// <summary>A release in the changelog: its version, date and what changed.</summary>
public sealed record Release(string Version, string Date, IReadOnlyList<string> Changes);

/// <summary>
/// «Τι νέο υπάρχει»: on the first start after an update, the releases since the version used
/// before (from the changelog built into the app, the same file athlotrack.gr/version shows).
/// Nothing to list, a fresh install, or the same version: no window.
/// </summary>
public static class WhatsNew
{
    public const string SeenKey = "whatsnew.seen_version";

    /// <summary>Set by the login when someone has signed in on this device before (a fresh install has none).</summary>
    public const string UsedBeforeKey = AthloTrack.ViewModels.LoginViewModel.LastRoleKey;

    /// <summary>The releases to show now (newest first), or none; remembers this version as seen.</summary>
    public static IReadOnlyList<Release> TakePending(IAppPreferences prefs, string current, IReadOnlyList<Release> changelog)
    {
        var seen = prefs.Get(SeenKey);
        prefs.Set(SeenKey, current);
        if (seen == current) return [];
        if (seen is null)
        {
            // A fresh install shows nothing; an update from before this window existed shows this release.
            if (prefs.Get(UsedBeforeKey) is null) return [];
            return changelog.Where(r => Compare(r.Version, current) == 0 && r.Changes.Count > 0).ToList();
        }
        return changelog
            .Where(r => Compare(r.Version, seen) > 0 && Compare(r.Version, current) <= 0 && r.Changes.Count > 0)
            .OrderByDescending(r => r.Version, Comparer<string>.Create(Compare))
            .ToList();
    }

    /// <summary>"1.27-alpha" vs "1.3": by the numbers before the dash (1.27 is after 1.3).</summary>
    public static int Compare(string a, string b)
    {
        var x = Numbers(a);
        var y = Numbers(b);
        for (var i = 0; i < Math.Max(x.Length, y.Length); i++)
        {
            var c = (i < x.Length ? x[i] : 0).CompareTo(i < y.Length ? y[i] : 0);
            if (c != 0) return c;
        }
        return 0;
    }

    private static int[] Numbers(string version)
    {
        var dash = version.IndexOf('-');
        var core = dash >= 0 ? version[..dash] : version;
        return core.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
    }

    /// <summary>The changelog built into the app (Assets/changelog.json).</summary>
    public static IReadOnlyList<Release> LoadBuiltIn()
    {
        try
        {
            using var stream = Avalonia.Platform.AssetLoader.Open(new Uri("avares://AthloTrack/Assets/changelog.json"));
            return Parse(stream);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AthloTrack] Changelog: {ex.Message}");
            return [];
        }
    }

    /// <summary>{"releases":[{"version","date","changes":[…]}]}; read without reflection (trimmed heads).</summary>
    public static IReadOnlyList<Release> Parse(Stream json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<Release>();
        if (!doc.RootElement.TryGetProperty("releases", out var releases)) return list;
        foreach (var r in releases.EnumerateArray())
        {
            var changes = r.TryGetProperty("changes", out var c) && c.ValueKind == JsonValueKind.Array
                ? c.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Trim().Length > 0).ToList()
                : new List<string>();
            list.Add(new Release(
                r.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "",
                r.TryGetProperty("date", out var d) ? d.GetString() ?? "" : "",
                changes));
        }
        return list;
    }

    /// <summary>The window's text in the app's markup: a heading per release, its changes as a list.</summary>
    public static string ToMarkup(IReadOnlyList<Release> releases) =>
        string.Join("\n\n", releases.Select(r =>
            $"## Έκδοση {r.Version}{(DateTime.TryParse(r.Date, out var at) ? $" · {at:dd/MM/yyyy}" : "")}\n"
            + string.Join("\n", r.Changes.Select(c => "- " + c))));
}
