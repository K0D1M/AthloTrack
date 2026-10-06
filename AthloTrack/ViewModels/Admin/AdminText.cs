using System;
using System.Globalization;

namespace AthloTrack.ViewModels;

/// <summary>Value converters for the admin views.</summary>
public static class AdminConverters
{
    /// <summary>Health checks with nothing to fix are dimmed.</summary>
    public static readonly Avalonia.Data.Converters.IValueConverter IssueOpacity =
        new Avalonia.Data.Converters.FuncValueConverter<bool, double>(hasIssues => hasIssues ? 1.0 : 0.55);
}

/// <summary>Greek wording for the admin dashboard: dates, "πριν από…", actions and events.</summary>
public static class AdminText
{
    private static readonly CultureInfo Greek = CultureInfo.GetCultureInfo("el-GR");

    public static string When(DateTimeOffset? at) =>
        at is null ? "—" : at.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Greek);

    /// <summary>"μόλις τώρα", "πριν 5 λεπτά", "πριν 3 ημέρες", or the date for older ones.</summary>
    public static string Ago(DateTimeOffset? at, DateTimeOffset? now = null)
    {
        if (at is null) return "ποτέ";
        var span = (now ?? DateTimeOffset.Now) - at.Value;
        if (span < TimeSpan.FromMinutes(1)) return "μόλις τώρα";
        if (span < TimeSpan.FromHours(1)) return Plural((int)span.TotalMinutes, "λεπτό", "λεπτά");
        if (span < TimeSpan.FromDays(1)) return Plural((int)span.TotalHours, "ώρα", "ώρες");
        if (span < TimeSpan.FromDays(30)) return Plural((int)span.TotalDays, "ημέρα", "ημέρες");
        return at.Value.ToLocalTime().ToString("dd/MM/yyyy", Greek);
    }

    private static string Plural(int n, string one, string many) => $"πριν {n} {(n == 1 ? one : many)}";

    /// <summary>Search match ignoring case and accents: «νικ» finds «Νίκος».</summary>
    public static bool Matches(string? text, string query) =>
        query.Length == 0 || (text is not null && Greek.CompareInfo.IndexOf(text, query,
            CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);

    /// <summary>"80%", or "—" when there's nothing to count.</summary>
    public static string Percent(int part, int whole) => whole <= 0 ? "—" : $"{(int)Math.Round(100.0 * part / whole)}%";

    /// <summary>An admin_audit action as the dashboard shows it.</summary>
    public static string Action(string action) => action switch
    {
        "create_coach" => "Νέος προπονητής",
        "reset_password" => "Νέος προσωρινός κωδικός",
        "delete_login" => "Διαγραφή λογαριασμού",
        "force_password_change" => "Επιβολή αλλαγής κωδικού",
        "move_athlete" => "Μεταφορά αθλητή",
        "delete_athlete" => "Διαγραφή αθλητή",
        "test_push" => "Δοκιμαστική ειδοποίηση",
        "broadcast" => "Ανακοίνωση",
        _ => action,
    };

    /// <summary>The kind of an admin_activity event, as a short label.</summary>
    public static string Kind(string kind) => kind switch
    {
        "notification:new_workout" => "Νέο ασκησιολόγιο",
        "notification:workout_updated" => "Αλλαγή ασκησιολογίου",
        "notification:workout_completed" => "Ολοκλήρωση",
        "measurement" => "Μέτρηση",
        "athlete" => "Νέος αθλητής",
        "signup" => "Νέος λογαριασμός",
        _ when kind.StartsWith("notification:", StringComparison.Ordinal) => "Ειδοποίηση",
        _ => kind,
    };

    public static string Role(string role) => role switch
    {
        "coach" => "Προπονητής",
        "athlete" => "Αθλητής",
        "admin" => "Διαχειριστής",
        _ => "Χωρίς προφίλ",
    };

    public static string Platform(string platform) => platform switch
    {
        "android" or "android-data" => "Android",
        "web" => "Browser",
        _ => platform,
    };
}
