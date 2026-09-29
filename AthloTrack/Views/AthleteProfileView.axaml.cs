using System;
using AthloTrack.Services;
using AthloTrack.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LiveChartsCore.SkiaSharpView.Avalonia;

namespace AthloTrack.Views;

public partial class AthleteProfileView : UserControl
{
    public AthleteProfileView()
    {
        InitializeComponent();

        // The chart is built only once its data exists. On Android a chart created empty and
        // filled in later intermittently never painted (data and size were fine).
        DataContextChanged += (_, _) =>
        {
            if (DataContext is AthleteProfileViewModel vm)
            {
                vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName is nameof(AthleteProfileViewModel.ChartSeries))
                    {
                        BuildChart(vm);
                    }
                };
                BuildChart(vm);
            }
        };
    }

    private void BuildChart(AthleteProfileViewModel vm)
    {
        if (vm.ChartSeries.Length == 0)
        {
            ChartHost.Child = null;
            return;
        }

        ChartHost.Child = new CartesianChart
        {
            Series = vm.ChartSeries,
            XAxes = vm.ChartXAxes,
            LegendPosition = LiveChartsCore.Measure.LegendPosition.Bottom,
        };
    }

    // ---- "+" in-page menu ----
    private void ShowAddMenu(bool show)
    {
        AddMenu.IsVisible = show;
        AddMenuDismiss.IsVisible = show;
    }

    private void OnToggleAddMenu(object? sender, RoutedEventArgs e) => ShowAddMenu(!AddMenu.IsVisible);

    private void OnAddMenuDismiss(object? sender, Avalonia.Input.PointerPressedEventArgs e) => ShowAddMenu(false);

    private void OnAddMeasurement(object? sender, RoutedEventArgs e)
    {
        ShowAddMenu(false);
        (DataContext as AthleteProfileViewModel)?.AddMeasurementCommand.Execute(null);
    }

    private void OnAddWorkout(object? sender, RoutedEventArgs e)
    {
        ShowAddMenu(false);
        (DataContext as AthleteProfileViewModel)?.AddWorkoutCommand.Execute(null);
    }

    // File picking and image decoding are UI concerns, so they live here; the view model only
    // receives the finished bytes.
    private async void OnChangePhoto(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AthleteProfileViewModel vm) return;
        try
        {
            var picked = await PhotoPicker.PickAvatarAsync(this);
            if (picked is null) return;
            if (picked.Jpeg is null)
            {
                vm.ErrorMessage = picked.Error;
                return;
            }
            await vm.UploadPhotoAsync(picked.Jpeg, PhotoProcessor.ContentType);
        }
        catch (Exception ex)
        {
            // async void: an escaped exception would take the app down.
            vm.ErrorMessage = $"Η φωτογραφία δεν αποθηκεύτηκε: {ex.Message}";
        }
    }
}
