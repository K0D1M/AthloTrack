using AthloTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AthloTrack.WinUI.Views;

public sealed partial class CalendarPage : Page
{
    public CalendarViewModel Vm { get; }

    public CalendarPage()
    {
        Vm = App.Services.GetRequiredService<CalendarViewModel>();
        InitializeComponent();
        List.ItemsSource = Vm.Entries;
        Vm.Entries.CollectionChanged += (_, _) => SyncEmpty();
        SyncEmpty();
    }

    private void SyncEmpty()
        => EmptyLabel.Visibility = Vm.Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
}
