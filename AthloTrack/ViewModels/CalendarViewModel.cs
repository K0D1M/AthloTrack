using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AthloTrack.Core.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

/// <summary>Ημερολόγιο — month grid with athlete photos on workout days, plus an agenda list.</summary>
public partial class CalendarViewModel : ViewModelBase
{
    private readonly IWorkoutRepository _workouts;
    private readonly IAthleteRepository _athletes;
    private readonly IAvatarService _avatars;
    private readonly CultureInfo _greek = CultureInfo.GetCultureInfo("el-GR");

    private DateOnly _month = FirstOfMonth(DateOnly.FromDateTime(DateTime.Today));

    public CalendarViewModel(IWorkoutRepository workouts, IAthleteRepository athletes, IAvatarService avatars)
    {
        _workouts = workouts;
        _athletes = athletes;
        _avatars = avatars;

        // Monday-first short day names: Δε Τρ Τε Πε Πα Σα Κυ.
        var names = _greek.DateTimeFormat.ShortestDayNames;
        WeekdayNames = Enumerable.Range(1, 7).Select(i => names[i % 7]).ToArray();

        _ = LoadAsync();
    }

    /// <summary>Every workout program, oldest first (the WinUI head lists these directly).</summary>
    public ObservableCollection<CalendarEntryViewModel> Entries { get; } = new();

    /// <summary>The 42 cells (6 weeks) of the displayed month, Monday first.</summary>
    public ObservableCollection<CalendarDayViewModel> Days { get; } = new();

    /// <summary>The agenda under the grid: the selected day, or the whole month.</summary>
    public ObservableCollection<CalendarEntryViewModel> AgendaEntries { get; } = new();

    public string[] WeekdayNames { get; }

    [ObservableProperty]
    public partial string MonthTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AgendaTitle { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoAgendaEntries))]
    public partial bool IsLoading { get; set; }

    public bool HasNoAgendaEntries => !IsLoading && AgendaEntries.Count == 0;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    private CalendarDayViewModel? _selectedDay;

    /// <summary>Raised when an agenda entry is tapped, so the shell can open that athlete.</summary>
    public event Action<Guid>? OpenAthleteRequested;

    [RelayCommand]
    private void PreviousMonth() => ShowMonth(_month.AddMonths(-1));

    [RelayCommand]
    private void NextMonth() => ShowMonth(_month.AddMonths(1));

    [RelayCommand]
    private void SelectDay(CalendarDayViewModel? day)
    {
        if (day is null) return;

        // Tapping the selected day again goes back to the whole month.
        var next = ReferenceEquals(day, _selectedDay) ? null : day;
        if (_selectedDay is not null) _selectedDay.IsSelected = false;
        _selectedDay = next;
        if (next is not null) next.IsSelected = true;

        // A day from the neighbouring month switches to that month.
        if (next is not null && !next.IsCurrentMonth)
        {
            ShowMonth(FirstOfMonth(next.Date), next.Date);
            return;
        }

        RefreshAgenda();
    }

    [RelayCommand]
    private void OpenEntry(CalendarEntryViewModel? entry)
    {
        if (entry is not null && entry.AthleteId != Guid.Empty)
        {
            OpenAthleteRequested?.Invoke(entry.AthleteId);
        }
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var athletes = await _athletes.GetAllAsync();
            var byId = athletes.ToDictionary(a => a.Id);

            var programs = await _workouts.GetAllAsync();
            Entries.Clear();
            foreach (var p in programs.OrderBy(p => p.TargetDate))
            {
                byId.TryGetValue(p.AthleteId, out var athlete);
                Entries.Add(new CalendarEntryViewModel(
                    p.TargetDate, p.AthleteId, athlete?.FullName ?? "—", athlete?.ProfileImagePath, p.Content));
            }

            ShowMonth(_month);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }

        // Photos last: one download per athlete, shared by all of their entries.
        foreach (var group in Entries.Where(e => e.ProfileImagePath is not null).GroupBy(e => e.ProfileImagePath))
        {
            var photo = await _avatars.GetAsync(group.Key);
            foreach (var entry in group) entry.Photo = photo;
        }
    }

    private void ShowMonth(DateOnly month, DateOnly? select = null)
    {
        _month = FirstOfMonth(month);
        MonthTitle = Capitalize(_month.ToString("MMMM yyyy", _greek));

        var today = DateOnly.FromDateTime(DateTime.Today);
        var offset = ((int)_month.DayOfWeek + 6) % 7; // Monday = 0
        var start = _month.AddDays(-offset);
        var byDate = Entries.GroupBy(e => e.Date).ToDictionary(g => g.Key, g => (IReadOnlyList<CalendarEntryViewModel>)g.ToList());

        _selectedDay = null;
        Days.Clear();
        for (var i = 0; i < 42; i++)
        {
            var date = start.AddDays(i);
            var entries = byDate.TryGetValue(date, out var list) ? list : Array.Empty<CalendarEntryViewModel>();
            var day = new CalendarDayViewModel(date, date.Month == _month.Month, date == today, entries);
            if (select == date)
            {
                day.IsSelected = true;
                _selectedDay = day;
            }
            Days.Add(day);
        }

        RefreshAgenda();
    }

    private void RefreshAgenda()
    {
        IEnumerable<CalendarEntryViewModel> items;
        if (_selectedDay is { } day)
        {
            AgendaTitle = Capitalize(day.Date.ToString("dddd d MMMM", _greek));
            items = day.Entries;
        }
        else
        {
            AgendaTitle = $"Προπονήσεις — {MonthTitle}";
            items = Entries.Where(e => e.Date.Year == _month.Year && e.Date.Month == _month.Month);
        }

        AgendaEntries.Clear();
        foreach (var e in items) AgendaEntries.Add(e);
        OnPropertyChanged(nameof(HasNoAgendaEntries));
    }

    private static DateOnly FirstOfMonth(DateOnly d) => new(d.Year, d.Month, 1);

    private string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], _greek) + s.Substring(1);
}
