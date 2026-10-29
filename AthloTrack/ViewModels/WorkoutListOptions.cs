using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AthloTrack.ViewModels;

/// <summary>Προπονήσεις: the chips above the list.</summary>
public enum WorkoutFilter { All, Today, Upcoming, Open, Answered }

/// <summary>Προπονήσεις: the «Ταξινόμηση» sheet.</summary>
public enum WorkoutSort { Smart, Newest, Oldest, Athlete, Status }

/// <summary>A filter chip: «Εκκρεμούν (2)».</summary>
public sealed partial class WorkoutFilterOption(WorkoutFilter filter, string label) : ObservableObject
{
    public WorkoutFilter Filter { get; } = filter;
    public string Label { get; } = label;

    [ObservableProperty]
    public partial int Count { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>A choice in the «Ταξινόμηση» sheet; <see cref="Short"/> is shown on the button.</summary>
public sealed partial class WorkoutSortOption(WorkoutSort sort, string label, string hint, string @short) : ObservableObject
{
    public WorkoutSort Sort { get; } = sort;
    public string Label { get; } = label;
    public string Hint { get; } = hint;
    public string Short { get; } = @short;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>A heading and its workouts: a day («Σήμερα · Τρίτη 28/10»), an athlete or a status.</summary>
public sealed class WorkoutGroup(string title, string subtitle, bool isToday, IReadOnlyList<WorkoutListItemViewModel> items)
{
    public string Title { get; } = title;
    public string Subtitle { get; } = subtitle;
    public bool HasSubtitle => Subtitle.Length > 0;
    public bool IsToday { get; } = isToday;
    public IReadOnlyList<WorkoutListItemViewModel> Items { get; } = items;
}

/// <summary>An athlete in the «Νέο ασκησιολόγιο» picker.</summary>
public sealed record AthleteChoice(Guid Id, string Name)
{
    public string Initial => Name.Length > 0 ? Name.Substring(0, 1).ToUpperInvariant() : "?";
}
