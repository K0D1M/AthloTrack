using System;
using System.ComponentModel;
using AthloTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AthloTrack.WinUI.Views;

public sealed partial class AddWorkoutPage : Page
{
    public AddWorkoutViewModel Vm { get; }
    public event Action? Completed;

    public AddWorkoutPage(Guid athleteId, string athleteName)
    {
        Vm = App.Services.GetRequiredService<AddWorkoutViewModelFactory>().Create(athleteId, athleteName);
        InitializeComponent();

        TitleText.Text = Vm.Title;
        DatePicker.Date = Vm.TargetDate;

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
        Vm.Content = ContentBox.Text;
        if (DatePicker.Date is { } d) Vm.TargetDate = d;
        Vm.SaveCommand.Execute(null);
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Vm.CancelCommand.Execute(null);
}
