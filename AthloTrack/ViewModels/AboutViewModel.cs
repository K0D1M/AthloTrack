using CommunityToolkit.Mvvm.ComponentModel;

namespace AthloTrack.ViewModels;

/// <summary>Σχετικά — about page.</summary>
public partial class AboutViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string AppName { get; set; } = "AthloTrack";

    [ObservableProperty]
    public partial string Version { get; set; } = "0.1.0";
}
