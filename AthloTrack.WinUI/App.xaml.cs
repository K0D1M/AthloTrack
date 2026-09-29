using System;
using AthloTrack.Core.Auth;
using AthloTrack.Core.DependencyInjection;
using AthloTrack.ViewModels;
using AthloTrack.WinUI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace AthloTrack.WinUI;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static MainWindow? Shell { get; private set; }

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            // WinUI kills the process on unobserved exceptions; keep it alive and visible.
            e.Handled = true;
            System.Diagnostics.Debug.WriteLine(e.Message);
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Services = BuildServices();
        Shell = new MainWindow();
        Shell.Activate();
    }

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddAthloTrackCore(AppBootstrap.LoadSupabaseConfig());
        services.AddSingleton<ICredentialStore, WinUiCredentialStore>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<RecentViewModel>();
        services.AddTransient<AthletesViewModel>();
        services.AddTransient<WorkoutsViewModel>();
        services.AddTransient<CalendarViewModel>();
        services.AddTransient<AboutViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AddAthleteViewModel>();
        services.AddSingleton<CurrentUserViewModel>();
        services.AddSingleton<AthleteProfileViewModelFactory>();
        services.AddSingleton<AddMeasurementViewModelFactory>();
        services.AddSingleton<AddWorkoutViewModelFactory>();

        return services.BuildServiceProvider();
    }
}
