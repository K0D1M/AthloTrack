using System.ComponentModel;
using AthloTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AthloTrack.WinUI.Views;

public sealed partial class RecentPage : Page
{
    public RecentViewModel Vm { get; }

    public RecentPage()
    {
        Vm = App.Services.GetRequiredService<RecentViewModel>();
        InitializeComponent();

        Welcome.Text = Vm.WelcomeMessage;
        RecentCard.Visibility = Vm.IsCoach ? Visibility.Visible : Visibility.Collapsed;
        NotifSection.Visibility = Vm.IsAthlete ? Visibility.Visible : Visibility.Collapsed;
        NotifList.ItemsSource = Vm.Notifications;

        Vm.PropertyChanged += OnVmPropertyChanged;
        Vm.Notifications.CollectionChanged += (_, _) => SyncNotifications();
        Sync();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e) => Sync();

    private void Sync()
    {
        Welcome.Text = Vm.WelcomeMessage;
        RecentName.Text = Vm.RecentAthleteName ?? string.Empty;
        RecentSummary.Text = Vm.RecentAthleteSummary ?? string.Empty;
        SyncNotifications();
    }

    private void SyncNotifications()
    {
        NoNotif.Visibility = Vm.Notifications.Count == 0 && Vm.IsAthlete
            ? Visibility.Visible : Visibility.Collapsed;
    }
}
