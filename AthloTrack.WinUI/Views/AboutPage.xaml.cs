using AthloTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AthloTrack.WinUI.Views;

public sealed partial class AboutPage : Page
{
    public AboutViewModel Vm { get; }

    public AboutPage()
    {
        Vm = App.Services.GetRequiredService<AboutViewModel>();
        InitializeComponent();
        AppNameText.Text = Vm.AppName;
        VersionText.Text = $"Έκδοση {Vm.Version}";
    }
}
