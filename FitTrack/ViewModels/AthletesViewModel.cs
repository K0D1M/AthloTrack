using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FitTrack.ViewModels;

/// <summary>Αθλητές — list of athletes.</summary>
public partial class AthletesViewModel : ViewModelBase
{
    public ObservableCollection<string> Athletes { get; } = new();
}
