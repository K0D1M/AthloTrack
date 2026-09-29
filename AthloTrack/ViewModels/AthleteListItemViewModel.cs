using System;
using AthloTrack.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AthloTrack.ViewModels;

public sealed partial class AthleteListItemViewModel : ObservableObject
{
    public AthleteListItemViewModel(Athlete athlete)
    {
        Id = athlete.Id;
        FullName = athlete.FullName;
        ProfileImagePath = athlete.ProfileImagePath;
        var bits = new System.Collections.Generic.List<string>();
        if (athlete.HeightCm is { } h) bits.Add($"{h} cm");
        if (athlete.DateOfBirth is { } dob) bits.Add($"{dob:dd/MM/yyyy}");
        Subtitle = string.Join("  •  ", bits);
    }

    public Guid Id { get; }
    public string FullName { get; }
    public string Subtitle { get; }
    public string? ProfileImagePath { get; }

    /// <summary>First letter of the name, shown until (or instead of) a photo.</summary>
    public string Initial => string.IsNullOrEmpty(FullName) ? "?" : FullName.Substring(0, 1).ToUpperInvariant();

    /// <summary>Profile photo bytes, loaded after the list renders; null means show the initial.</summary>
    [ObservableProperty]
    public partial byte[]? Photo { get; set; }
}
