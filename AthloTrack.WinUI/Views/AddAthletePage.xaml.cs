using System;
using System.ComponentModel;
using AthloTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AthloTrack.WinUI.Views;

public sealed partial class AddAthletePage : Page
{
    public AddAthleteViewModel Vm { get; }
    public event Action? Completed;

    public AddAthletePage()
    {
        Vm = App.Services.GetRequiredService<AddAthleteViewModel>();
        InitializeComponent();

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
        Vm.FullName = NameBox.Text;
        Vm.HeightCm = HeightBox.Text;
        Vm.Notes = NotesBox.Text;
        Vm.DateOfBirth = DobPicker.Date;
        Vm.SaveCommand.Execute(null);
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Vm.CancelCommand.Execute(null);
}
