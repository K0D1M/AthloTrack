using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;

namespace AthloTrack.Services;

/// <summary>A newer version and where to download it.</summary>
public sealed record UpdateInfo(string Version, string Url);

/// <summary>
/// «Νέα έκδοση»: reads athlotrack.gr/version/latest.json and compares the platform's newest version
/// with this app's (<see cref="ViewModels.AppInfo.Version"/>). Only heads that set <see cref="Channel"/>
/// check (the Android APK; the website updates itself).
/// </summary>
public static class UpdateChecker
{
    public const string LatestUrl = "https://athlotrack.gr/version/latest.json";

    private const string LastCheckKey = "update.last_check";
    private const string DismissedKey = "update.dismissed";

    /// <summary>The key in latest.json for this head ("android"), or null: no update check.</summary>
    public static string? Channel { get; set; }

    public static bool IsSupported => Channel is not null;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>
    /// The newer version, or null (up to date, no link yet, offline). Throws only when
    /// <paramref name="throwOnError"/> (the «Έλεγχος» button shows the failure).
    /// </summary>
    public static async Task<UpdateInfo?> CheckAsync(bool throwOnError = false, CancellationToken ct = default)
    {
        if (Channel is not { } channel) return null;
        try
        {
            // The query keeps any cache in between from answering with an old copy.
            var json = await Http.GetStringAsync($"{LatestUrl}?t={DateTime.UtcNow.Ticks}", ct);
            return Parse(json, channel, ViewModels.AppInfo.Version);
        }
        catch when (!throwOnError)
        {
            return null;
        }
    }

    /// <summary>The start-up check: at most once a day, and not for a version the user dismissed.</summary>
    public static async Task<UpdateInfo?> CheckDailyAsync(IAppPreferences prefs, DateTimeOffset now)
    {
        if (!IsSupported) return null;
        if (DateTimeOffset.TryParse(prefs.Get(LastCheckKey), CultureInfo.InvariantCulture, DateTimeStyles.None, out var last)
            && now - last < TimeSpan.FromHours(20))
        {
            return null;
        }
        var update = await CheckAsync();
        prefs.Set(LastCheckKey, now.ToString("O", CultureInfo.InvariantCulture));
        return update is not null && update.Version != prefs.Get(DismissedKey) ? update : null;
    }

    /// <summary>Opens a link in the browser (MainView sets it: it has the top level's launcher).</summary>
    public static Func<string, Task>? Opener { get; set; }

    public static Task OpenAsync(UpdateInfo update) => Opener?.Invoke(update.Url) ?? Task.CompletedTask;

    /// <summary>«Αργότερα» on the banner: don't show it again for this version.</summary>
    public static void Dismiss(IAppPreferences prefs, UpdateInfo update) => prefs.Set(DismissedKey, update.Version);

    /// <summary>latest.json: {"android": {"version": "1.20-alpha", "url": "https://mega.nz/..."}}.</summary>
    public static UpdateInfo? Parse(string json, string channel, string current)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty(channel, out var entry) || entry.ValueKind != JsonValueKind.Object) return null;
        var version = entry.TryGetProperty("version", out var v) ? v.GetString() : null;
        var url = entry.TryGetProperty("url", out var u) ? u.GetString() : null;
        if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(url)) return null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return null;
        return IsNewer(version, current) ? new UpdateInfo(version.Trim(), url.Trim()) : null;
    }

    /// <summary>"1.20-alpha" &gt; "1.19-alpha": the numbers before the dash, part by part.</summary>
    public static bool IsNewer(string candidate, string current)
    {
        var a = Numbers(candidate);
        var b = Numbers(current);
        for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            var x = i < a.Length ? a[i] : 0;
            var y = i < b.Length ? b[i] : 0;
            if (x != y) return x > y;
        }
        return false;
    }

    private static int[] Numbers(string version)
    {
        var core = version.Trim();
        var dash = core.IndexOfAny(['-', '+']);
        if (dash >= 0) core = core[..dash];
        var parts = core.Split('.');
        var numbers = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            numbers[i] = int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;
        return numbers;
    }
}
