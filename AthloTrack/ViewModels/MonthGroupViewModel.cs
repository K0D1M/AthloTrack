using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AthloTrack.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

/// <summary>
/// One month of an athlete's measurements or workouts, opened and closed with its header
/// (chevron + «Οκτώβριος 2026 (3)»). The first group of each year carries the year separator.
/// </summary>
public abstract partial class MonthGroup<T> : ObservableObject
{
    protected MonthGroup(int year, int month, IReadOnlyList<T> items, bool isExpanded, bool showYearSeparator)
    {
        Year = year;
        Month = month;
        Items = items;
        IsExpanded = isExpanded;
        ShowYearSeparator = showYearSeparator;
        var name = MonthGroups.Greek.DateTimeFormat.GetMonthName(month);
        Title = $"{char.ToUpper(name[0], MonthGroups.Greek)}{name[1..]} {year}";
    }

    public int Year { get; }
    public int Month { get; }
    public string Key => $"{Year:D4}-{Month:D2}";
    public string Title { get; }
    public IReadOnlyList<T> Items { get; }
    public int Count => Items.Count;
    public string Header => $"{Title} ({Count})";

    /// <summary>A «── 2025 ──» line above this group: the year changes here (not shown for the newest year).</summary>
    public bool ShowYearSeparator { get; }
    public string YearText => Year.ToString(CultureInfo.InvariantCulture);

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;
}

/// <summary>A month of measurements (concrete type: compiled XAML bindings can't name a generic).</summary>
public sealed class MeasurementMonth(int year, int month, IReadOnlyList<Measurement> items, bool isExpanded, bool showYearSeparator)
    : MonthGroup<Measurement>(year, month, items, isExpanded, showYearSeparator);

/// <summary>A month of workouts.</summary>
public sealed class WorkoutMonth(int year, int month, IReadOnlyList<WorkoutProgram> items, bool isExpanded, bool showYearSeparator)
    : MonthGroup<WorkoutProgram>(year, month, items, isExpanded, showYearSeparator);

public static class MonthGroups
{
    internal static readonly CultureInfo Greek = CultureInfo.GetCultureInfo("el-GR");

    /// <summary>
    /// Groups by month, newest first, items newest first within each month. The newest month
    /// starts open and the rest closed; <paramref name="previous"/> (the groups before a reload)
    /// keeps whatever the user opened or closed.
    /// </summary>
    public static List<MeasurementMonth> Build(IEnumerable<Measurement> items, IEnumerable<MeasurementMonth>? previous = null) =>
        Build(items, m => m.MeasuredAt, (y, m, list, open, sep) => new MeasurementMonth(y, m, list, open, sep), previous);

    public static List<WorkoutMonth> Build(IEnumerable<WorkoutProgram> items, IEnumerable<WorkoutMonth>? previous = null) =>
        Build(items, w => w.TargetDate, (y, m, list, open, sep) => new WorkoutMonth(y, m, list, open, sep), previous);

    private static List<TGroup> Build<T, TGroup>(IEnumerable<T> items, Func<T, DateOnly> dateOf,
        Func<int, int, List<T>, bool, bool, TGroup> create, IEnumerable<TGroup>? previous)
        where TGroup : MonthGroup<T>
    {
        var expanded = previous?.ToDictionary(g => g.Key, g => g.IsExpanded) ?? new Dictionary<string, bool>();
        var groups = new List<TGroup>();
        int? newestYear = null;
        int? lastYear = null;

        foreach (var month in items
                     .OrderByDescending(dateOf)
                     .GroupBy(i => (dateOf(i).Year, dateOf(i).Month)))
        {
            var (year, number) = month.Key;
            newestYear ??= year;
            var key = $"{year:D4}-{number:D2}";
            var isExpanded = expanded.TryGetValue(key, out var was) ? was : groups.Count == 0;
            var separator = lastYear != year && year != newestYear;
            groups.Add(create(year, number, month.ToList(), isExpanded, separator));
            lastYear = year;
        }

        return groups;
    }
}
