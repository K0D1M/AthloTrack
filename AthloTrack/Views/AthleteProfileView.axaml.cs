using System;
using System.Collections.Generic;
using System.Linq;
using AthloTrack.Services;
using AthloTrack.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AthloTrack.Views;

public partial class AthleteProfileView : UserControl, IHandlesBack
{
    /// <summary>Back with the "+" menu open closes the menu, like a native menu.</summary>
    public bool TryHandleBack()
    {
        if (!AddMenu.IsVisible) return false;
        ShowAddMenu(false);
        return true;
    }

    public AthleteProfileView()
    {
        InitializeComponent();

        // The chart is (re)built from the view model's plain data whenever it changes.
        DataContextChanged += (_, _) =>
        {
            if (DataContext is AthleteProfileViewModel vm)
            {
                vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName is nameof(AthleteProfileViewModel.Chart))
                    {
                        BuildChart(vm.Chart);
                    }
                };
                BuildChart(vm.Chart);
            }
        };
        // Light/Dark switched in Ρυθμίσεις: redraw the chart image in the new colours.
        ActualThemeVariantChanged += (_, _) => BuildChart((DataContext as AthleteProfileViewModel)?.Chart);
    }

    private void BuildChart(ProgressChart? chart)
    {
        if (chart is null)
        {
            ChartHost.Child = null;
            return;
        }

        var plot = new ScottPlot.Plot();
        plot.Font.Set(ChartFonts.Family);

        foreach (var line in chart.Lines)
        {
            // Measurements without this metric are left out of its line.
            var xs = new List<double>();
            var ys = new List<double>();
            for (var i = 0; i < line.Values.Length; i++)
            {
                if (line.Values[i] is { } v) { xs.Add(i); ys.Add(v); }
            }
            if (xs.Count == 0) continue;

            var color = ScottPlot.Color.FromHex(line.ColorHex);
            var scatter = plot.Add.Scatter(xs.ToArray(), ys.ToArray());
            scatter.Color = color;
            scatter.LineWidth = (float)line.Width;
            scatter.MarkerSize = 7;
            scatter.LegendText = line.Name;
            if (line.Fill)
            {
                scatter.FillY = true;
                scatter.FillYColor = color.WithAlpha(0.15);
            }
        }

        // Dates as x labels, light grid, no frame on top/right, legend underneath.
        plot.Axes.Bottom.SetTicks(Enumerable.Range(0, chart.Labels.Length).Select(i => (double)i).ToArray(), chart.Labels);
        plot.Axes.Bottom.TickLabelStyle.FontSize = 11;
        plot.Axes.Left.TickLabelStyle.FontSize = 11;
        plot.Axes.Top.FrameLineStyle.Width = 0;
        plot.Axes.Right.FrameLineStyle.Width = 0;
        var dark = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        plot.Grid.MajorLineColor = ScottPlot.Color.FromHex(dark ? "#2E343C" : "#E8EEF6");
        plot.FigureBackground.Color = ScottPlot.Colors.Transparent;
        plot.DataBackground.Color = ScottPlot.Colors.Transparent;
        plot.Axes.Color(ScottPlot.Color.FromHex(dark ? "#C9CED6" : "#333333"));
        plot.ShowLegend(ScottPlot.Edge.Bottom);
        plot.Legend.FontSize = 12;
        if (dark)
        {
            plot.Legend.BackgroundColor = ScottPlot.Color.FromHex("#1E2228");
            plot.Legend.FontColor = ScottPlot.Color.FromHex("#E6E8EB");
            plot.Legend.OutlineColor = ScottPlot.Color.FromHex("#2E343C");
        }
        plot.Axes.Margins(horizontal: 0.05, vertical: 0.15);

        // A static image: nothing to pan or zoom, so dragging scrolls the page as usual.
        ChartHost.Child = new Views.Controls.PlotView { Plot = plot };
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
