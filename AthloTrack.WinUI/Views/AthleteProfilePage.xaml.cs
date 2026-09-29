using System;
using System.ComponentModel;
using AthloTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AthloTrack.WinUI.Views;

public sealed partial class AthleteProfilePage : Page
{
    public AthleteProfileViewModel Vm { get; }

    public event Action? BackRequested;
    public event Action<Guid>? AddMeasurementRequested;
    public event Action<Guid, string>? AddWorkoutRequested;

    public AthleteProfilePage(Guid athleteId)
    {
        Vm = App.Services.GetRequiredService<AthleteProfileViewModelFactory>().Create(athleteId);
        InitializeComponent();

        MeasurementList.ItemsSource = Vm.Measurements;
        WorkoutList.ItemsSource = Vm.Workouts;
        AddButton.Visibility = Vm.CanEdit ? Visibility.Visible : Visibility.Collapsed;

        Vm.AddMeasurementRequested += id => AddMeasurementRequested?.Invoke(id);
        Vm.AddWorkoutRequested += (id, name) => AddWorkoutRequested?.Invoke(id, name);

        Vm.PropertyChanged += OnVmPropertyChanged;
        Vm.Measurements.CollectionChanged += (_, _) => SyncLists();
        Vm.Workouts.CollectionChanged += (_, _) => SyncLists();

        Sync();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e) => Sync();

    private void Sync()
    {
        NameText.Text = Vm.FullName;
        SubtitleText.Text = Vm.Subtitle ?? string.Empty;

        ErrorBar.Message = Vm.ErrorMessage ?? string.Empty;
        ErrorBar.IsOpen = !string.IsNullOrWhiteSpace(Vm.ErrorMessage);

        // chart
        Chart.Series = Vm.ChartSeries;
        Chart.XAxes = Vm.ChartXAxes;
        ChartSection.Visibility = Vm.HasChartData ? Visibility.Visible : Visibility.Collapsed;

        SyncLists();
    }

    private void SyncLists()
    {
        NoMeasurements.Visibility = Vm.Measurements.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        NoWorkouts.Visibility = Vm.Workouts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ChartSection.Visibility = Vm.HasChartData ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnBack(object sender, RoutedEventArgs e) => BackRequested?.Invoke();

    private void OnAddMeasurement(object sender, RoutedEventArgs e)
        => Vm.AddMeasurementCommand.Execute(null);

    private void OnAddWorkout(object sender, RoutedEventArgs e)
        => Vm.AddWorkoutCommand.Execute(null);
}
