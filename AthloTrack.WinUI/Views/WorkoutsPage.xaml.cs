using AthloTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AthloTrack.WinUI.Views;

public sealed partial class WorkoutsPage : Page
{
    public WorkoutsViewModel Vm { get; }

    public WorkoutsPage()
    {
        Vm = App.Services.GetRequiredService<WorkoutsViewModel>();
        InitializeComponent();
        List.ItemsSource = Vm.WorkoutPrograms;
        Vm.WorkoutPrograms.CollectionChanged += (_, _) => SyncEmpty();
        SyncEmpty();
    }

    private void SyncEmpty()
        => EmptyLabel.Visibility = Vm.WorkoutPrograms.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
}
