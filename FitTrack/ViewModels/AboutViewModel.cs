using CommunityToolkit.Mvvm.ComponentModel;

namespace FitTrack.ViewModels;

/// <summary>Σχετικά — about page.</summary>
public partial class AboutViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string AppName { get; set; } = "FitTrack";

    [ObservableProperty]
    public partial string Version { get; set; } = "0.1.0";
}
