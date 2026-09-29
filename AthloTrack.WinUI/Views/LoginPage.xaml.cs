using System;
using System.ComponentModel;
using AthloTrack.Core.Auth;
using AthloTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AthloTrack.WinUI.Views;

public sealed partial class LoginPage : Page
{
    public LoginViewModel Vm { get; }

    private UserRole _role = UserRole.Coach;

    /// <summary>Raised once sign-in succeeds so the shell can swap in the main UI.</summary>
    public event Action<UserRole>? LoginSucceeded;

    public LoginPage()
    {
        Vm = App.Services.GetRequiredService<LoginViewModel>();
        InitializeComponent();

        Vm.PropertyChanged += OnVmPropertyChanged;
        Vm.LoginSucceeded += role => LoginSucceeded?.Invoke(role);

        SyncError();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LoginViewModel.ErrorMessage)) SyncError();
        if (e.PropertyName == nameof(LoginViewModel.IsBusy)) SyncBusy();
    }

    private void SyncError()
    {
        var msg = Vm.ErrorMessage;
        ErrorBar.Message = msg ?? string.Empty;
        ErrorBar.IsOpen = !string.IsNullOrWhiteSpace(msg);
    }

    private void SyncBusy()
    {
        var busy = Vm.IsBusy;
        SignInButton.IsEnabled = !busy;
        SignInSpinner.IsActive = busy;
        SignInSpinner.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        SignInLabel.Visibility = busy ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnSelectCoach(object sender, RoutedEventArgs e)
    {
        _role = UserRole.Coach;
        CoachToggle.Style = (Style)Application.Current.Resources["BrandPrimaryButton"];
        AthleteToggle.Style = (Style)Application.Current.Resources["BrandGhostButton"];
    }

    private void OnSelectAthlete(object sender, RoutedEventArgs e)
    {
        _role = UserRole.Athlete;
        AthleteToggle.Style = (Style)Application.Current.Resources["BrandAccentButton"];
        CoachToggle.Style = (Style)Application.Current.Resources["BrandGhostButton"];
    }

    private void OnSignIn(object sender, RoutedEventArgs e)
    {
        var command = _role == UserRole.Coach ? Vm.LoginAsCoachCommand : Vm.LoginAsAthleteCommand;
        if (command.CanExecute(null)) command.Execute(null);
    }
}
