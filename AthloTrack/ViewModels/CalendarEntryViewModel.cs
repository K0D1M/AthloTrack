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

    public CalendarEntryViewModel(DateOnly date, Guid athleteId, string athleteName, string? profileImagePath, string content)
    {
        Date = date;
        DateText = date.ToString("dddd dd/MM/yyyy", new System.Globalization.CultureInfo("el-GR"));
        AthleteId = athleteId;
        AthleteName = athleteName;
        ProfileImagePath = profileImagePath;
        Content = content;
    }

    public DateOnly Date { get; }
    public string DateText { get; }
    public Guid AthleteId { get; }
    public string AthleteName { get; }
    public string? ProfileImagePath { get; }
    public string Content { get; }

    /// <summary>First letter of the athlete's name — shown when there is no photo.</summary>
    public string Initial => string.IsNullOrEmpty(AthleteName) ? "?" : AthleteName.Substring(0, 1).ToUpperInvariant();

    /// <summary>The athlete's photo bytes; null shows <see cref="Initial"/>.</summary>
    [ObservableProperty]
    public partial byte[]? Photo { get; set; }
}
