using CommunityToolkit.Mvvm.ComponentModel;

namespace FitTrack.ViewModels;

/// <summary>Ρυθμίσεις — app settings (language, logout).</summary>
public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string SelectedLanguage { get; set; } = "el";
}
