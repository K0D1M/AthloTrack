using System;
using System.Globalization;

namespace AthloTrack.ViewModels;

/// <summary>Value converters over <see cref="TimeText"/>.</summary>
public static class TimeConverters
{
    /// <summary>A notification's time: «πριν 5 λεπτά» within a day, otherwise «07/10 18:32».</summary>
    public static readonly Avalonia.Data.Converters.IValueConverter Stamp =
        new Avalonia.Data.Converters.FuncValueConverter<DateTimeOffset, string>(at => TimeText.Stamp(at));
}

/// <summary>Greek wording for times: "πριν 5 λεπτά", dates, notification stamps.</summary>
public static class TimeText
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

    /// <summary>A notification's time: "πριν 5 λεπτά" within a day, otherwise "07/10 18:32".</summary>
    public static string Stamp(DateTimeOffset at, DateTimeOffset? now = null)
    {
        var span = (now ?? DateTimeOffset.Now) - at;
        return span < TimeSpan.FromDays(1) ? Ago(at, now) : at.ToLocalTime().ToString("dd/MM HH:mm", Greek);
    }

    private static string Plural(int n, string one, string many) => $"πριν {n} {(n == 1 ? one : many)}";
}
