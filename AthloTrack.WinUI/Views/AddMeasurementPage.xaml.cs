using System;
using System.ComponentModel;
using AthloTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AthloTrack.WinUI.Views;

public sealed partial class AddMeasurementPage : Page
{
    public AddMeasurementViewModel Vm { get; }
    public event Action? Completed;

    public AddMeasurementPage(Guid athleteId)
    {
        Vm = App.Services.GetRequiredService<AddMeasurementViewModelFactory>().Create(athleteId);
        InitializeComponent();

        DatePicker.Date = Vm.MeasuredAt;
        Vm.Saved += () => Completed?.Invoke();
        Vm.Cancelled += () => Completed?.Invoke();
        Vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ErrorBar.Message = Vm.ErrorMessage ?? string.Empty;
        ErrorBar.IsOpen = !string.IsNullOrWhiteSpace(Vm.ErrorMessage);
        SaveButton.IsEnabled = !Vm.IsBusy;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        Vm.WeightKg = WeightBox.Text;
        Vm.FatMassWt = FatMassBox.Text;
        Vm.FatHgt = FatHgtBox.Text;
        if (DatePicker.Date is { } d) Vm.MeasuredAt = d;
        Vm.SaveCommand.Execute(null);
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Vm.CancelCommand.Execute(null);
}
