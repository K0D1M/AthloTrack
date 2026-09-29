using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AthloTrack.ViewModels;

/// <summary>One cell of the month grid.</summary>
public sealed partial class CalendarDayViewModel : ObservableObject
{
    private const int MaxAvatars = 2;

    public CalendarDayViewModel(DateOnly date, bool isCurrentMonth, bool isToday, IReadOnlyList<CalendarEntryViewModel> entries)
    {
        Date = date;
        IsCurrentMonth = isCurrentMonth;
        IsToday = isToday;
        Entries = entries;
        Avatars = entries.Take(MaxAvatars).ToList();
        MoreCount = Math.Max(0, entries.Count - MaxAvatars);
    }

    public DateOnly Date { get; }
    public int DayNumber => Date.Day;
    public bool IsCurrentMonth { get; }
    public bool IsToday { get; }

    public IReadOnlyList<CalendarEntryViewModel> Entries { get; }
    public bool HasWorkouts => Entries.Count > 0;

    /// <summary>Up to two athletes drawn as photo circles in the cell.</summary>
    public IReadOnlyList<CalendarEntryViewModel> Avatars { get; }

    /// <summary>Workouts beyond the drawn avatars, shown as "+N".</summary>
    public int MoreCount { get; }
    public bool HasMore => MoreCount > 0;
    public string MoreText => $"+{MoreCount}";

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
