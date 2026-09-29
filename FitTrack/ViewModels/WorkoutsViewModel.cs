using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FitTrack.ViewModels;

/// <summary>Προπονήσεις — list of all current workout programs across athletes.</summary>
public partial class WorkoutsViewModel : ViewModelBase
{
    public ObservableCollection<string> WorkoutPrograms { get; } = new();
}
