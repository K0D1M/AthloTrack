using System;
using AthloTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AthloTrack.WinUI.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel Vm { get; }
    public event Action? LoggedOut;

    public SettingsPage()
    {
        Vm = App.Services.GetRequiredService<SettingsViewModel>();
        InitializeComponent();

        UserText.Text = Vm.CurrentUser;
        Vm.LoggedOut += () => LoggedOut?.Invoke();
    }

    private void OnLogout(object sender, RoutedEventArgs e) => Vm.LogoutCommand.Execute(null);
}
