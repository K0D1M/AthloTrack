using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AthloTrack.ViewModels;

/// <summary>One workout program on a date, with its athlete.</summary>
public sealed partial class CalendarEntryViewModel : ObservableObject
{
    public CalendarEntryViewModel(DateOnly date, string athleteName, string content)
        : this(date, Guid.Empty, athleteName, null, content)
    {
    }

    /// <param name="completed">The athlete's answer: true «Ολοκληρώθηκε», false «Δεν ολοκληρώθηκε», null none yet.</param>
    public CalendarEntryViewModel(DateOnly date, Guid athleteId, string athleteName, string? profileImagePath, string content,
        TimeOnly? startTime = null, bool? completed = null, Guid? workoutId = null)
    {
        Date = date;
        WorkoutId = workoutId;
        StartTime = startTime;
        IsDone = completed == true;
        IsNotDone = completed == false;
        DateText = date.ToString("dddd dd/MM/yyyy", new System.Globalization.CultureInfo("el-GR"))
                   + (startTime is { } t ? $" · {t:HH:mm}" : string.Empty);
        AthleteId = athleteId;
        AthleteName = athleteName;
        ProfileImagePath = profileImagePath;
        Content = content;
    }

    public DateOnly Date { get; }

    /// <summary>The workout itself: tapping the entry opens the profile at it.</summary>
    public Guid? WorkoutId { get; }

    /// <summary>The workout's optional start time (shown after the date).</summary>
    public TimeOnly? StartTime { get; }
    public string DateText { get; }
    public Guid AthleteId { get; }
    public string AthleteName { get; }
    public string? ProfileImagePath { get; }
    public string Content { get; }

    /// <summary>The green ✓ on the athlete's photo.</summary>
    public bool IsDone { get; }

    /// <summary>The red ✕ on the athlete's photo.</summary>
    public bool IsNotDone { get; }

    public bool IsAnswered => IsDone || IsNotDone;
    public string StatusText => IsDone ? "Ολοκληρώθηκε" : IsNotDone ? "Δεν ολοκληρώθηκε" : string.Empty;

    /// <summary>First letter of the athlete's name — shown when there is no photo.</summary>
    public string Initial => string.IsNullOrEmpty(AthleteName) ? "?" : AthleteName.Substring(0, 1).ToUpperInvariant();

    /// <summary>The athlete's photo bytes; null shows <see cref="Initial"/>.</summary>
    [ObservableProperty]
    public partial byte[]? Photo { get; set; }
}
