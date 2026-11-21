using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AthloTrack.ViewModels;

/// <summary>One cell of the month grid.</summary>
public sealed partial class CalendarDayViewModel : ObservableObject
{
    /// <summary>Two photos fit a phone cell; on a measurement day the ruler takes the second place.</summary>
    private const int MaxAvatars = 2;

    public CalendarDayViewModel(DateOnly date, bool isCurrentMonth, bool isToday, IReadOnlyList<CalendarEntryViewModel> entries,
        IReadOnlyList<CalendarEntryViewModel>? measurements = null)
    {
        Date = date;
        IsCurrentMonth = isCurrentMonth;
        IsToday = isToday;
        Entries = entries;
        Measurements = measurements ?? Array.Empty<CalendarEntryViewModel>();
        var avatars = Measurements.Count > 0 ? MaxAvatars - 1 : MaxAvatars;
        Avatars = entries.Take(avatars).ToList();
        MoreCount = Math.Max(0, entries.Count - avatars);
    }

    public DateOnly Date { get; }
    public int DayNumber => Date.Day;
    public bool IsCurrentMonth { get; }
    public bool IsToday { get; }

    public IReadOnlyList<CalendarEntryViewModel> Entries { get; }
    public bool HasWorkouts => Entries.Count > 0;

    /// <summary>The day's measurements (several a day are allowed), oldest first.</summary>
    public IReadOnlyList<CalendarEntryViewModel> Measurements { get; }
    public bool HasMeasurements => Measurements.Count > 0;

    /// <summary>The count on the ruler icon, only when there is more than one.</summary>
    public bool HasManyMeasurements => Measurements.Count > 1;
    public string MeasurementCountText => Measurements.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Workouts first, then measurements: the agenda for this day.</summary>
    public IEnumerable<CalendarEntryViewModel> AllEntries => Entries.Concat(Measurements);

    /// <summary>Up to two athletes drawn as photo circles in the cell.</summary>
    public IReadOnlyList<CalendarEntryViewModel> Avatars { get; }

    /// <summary>The photos and the ruler row.</summary>
    public bool HasMarks => HasWorkouts || HasMeasurements;

    /// <summary>Workouts beyond the drawn avatars, shown as "+N".</summary>
    public int MoreCount { get; }
    public bool HasMore => MoreCount > 0;
    public string MoreText => $"+{MoreCount}";

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
