using CommunityToolkit.Mvvm.ComponentModel;

namespace FitTrack.ViewModels;

/// <summary>Ημερολόγιο — calendar of workout dates marked with the athlete's photo.</summary>
public partial class CalendarViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial System.DateTime SelectedDate { get; set; } = System.DateTime.Today;
}
