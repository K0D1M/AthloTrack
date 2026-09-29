using System;
using System.ComponentModel;
using AthloTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AthloTrack.WinUI.Views;

public sealed partial class AthletesPage : Page
{
    public AthletesViewModel Vm { get; }

    public event Action<Guid>? OpenAthleteRequested;
    public event Action? AddAthleteRequested;

    public AthletesPage()
    {
        Vm = App.Services.GetRequiredService<AthletesViewModel>();
        InitializeComponent();

        List.ItemsSource = Vm.Athletes;
        AddButton.Visibility = Vm.CanEdit ? Visibility.Visible : Visibility.Collapsed;

        Vm.Athletes.CollectionChanged += (_, _) => SyncEmpty();
        Vm.PropertyChanged += OnVmPropertyChanged;
        SyncEmpty();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e) => SyncEmpty();

    private void SyncEmpty()
    {
        EmptyLabel.Visibility = Vm.Athletes.Count == 0 && !Vm.IsLoading
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnAthleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AthleteListItemViewModel item })
            OpenAthleteRequested?.Invoke(item.Id);
    }

    private void OnAddClick(object sender, RoutedEventArgs e) => AddAthleteRequested?.Invoke();
}
