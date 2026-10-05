namespace AthloTrack.ViewModels;

/// <summary>Ρυθμίσεις → Θέμα. System follows the phone's or computer's dark-mode setting.</summary>
public enum ThemeChoice
{
    System,
    Light,
    Dark,
}

/// <summary>Applies and remembers (per device) the chosen theme; implemented in Services/ThemeService.cs.</summary>
public interface IThemeService
{
    ThemeChoice Current { get; }
    void Set(ThemeChoice choice);
}
