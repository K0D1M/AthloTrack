using CommunityToolkit.Mvvm.ComponentModel;

namespace FitTrack.ViewModels;

/// <summary>Πρόσφατα — welcome screen showing the most recently updated athlete.</summary>
public partial class RecentViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string WelcomeMessage { get; set; } = "Χαίρε!";

    [ObservableProperty]
    public partial string? RecentAthleteName { get; set; }

    [ObservableProperty]
    public partial string? RecentAthleteSummary { get; set; }
}
