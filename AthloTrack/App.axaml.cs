using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AthloTrack.Core.Auth;
using AthloTrack.Core.DependencyInjection;
using AthloTrack.ViewModels;
using AthloTrack.Views;
using LiveChartsCore.SkiaSharpView;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace AthloTrack;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public static App? Instance { get; private set; }

    public override void OnFrameworkInitializationCompleted()
    {
        Instance = this;
        UseGreekCulture();
        BuildServices();
        ConfigureCharts();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Window
            {
                Title = "AthloTrack",
                Width = 420,
                Height = 720,
                Content = CreateLoginView(),
            };
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime activity)
        {
            activity.MainViewFactory = CreateLoginView;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = CreateLoginView();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Greek month/day names in DatePicker and Calendar, Monday-first weeks, and Greek number
    /// formatting on every head, regardless of the device's locale.
    /// </summary>
    private static void UseGreekCulture()
    {
        var greek = System.Globalization.CultureInfo.GetCultureInfo("el-GR");
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = greek;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = greek;
        System.Globalization.CultureInfo.CurrentCulture = greek;
        System.Globalization.CultureInfo.CurrentUICulture = greek;
    }

    /// <summary>
    /// Gives LiveCharts the bundled Inter typeface. The browser head has no system fonts,
    /// so chart legends and axis labels otherwise fell back to a monospace face.
    /// </summary>
    private static void ConfigureCharts()
    {
        try
        {
            using var stream = Avalonia.Platform.AssetLoader.Open(
                new Uri("avares://Avalonia.Fonts.Inter/Assets/Inter-Regular.ttf"));
            var buffer = new System.IO.MemoryStream();
            stream.CopyTo(buffer);
            buffer.Position = 0;
            var typeface = SkiaSharp.SKTypeface.FromStream(buffer);
            // Obsolete in favour of SKFontManager lookup, but under WASM the font manager is
            // empty, so the typeface has to be supplied explicitly.
#pragma warning disable CS0618
            LiveChartsCore.LiveCharts.Configure(config => config.HasGlobalSKTypeface(typeface));
#pragma warning restore CS0618
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AthloTrack] Chart typeface setup failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private Control CreateLoginView()
    {
        var loginVm = Services.GetRequiredService<LoginViewModel>();
        loginVm.LoginSucceeded += OnLoginSucceeded;
        // Deferred so the window/root view exists before a restored session navigates away.
        Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = loginVm.TryRestoreSessionAsync());
        return new LoginView { DataContext = loginVm };
    }

    /// <summary>Called on logout — resets the app root back to the login screen.</summary>
    public void NavigateToLogin()
    {
        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop when desktop.MainWindow is not null:
                desktop.MainWindow.Content = CreateLoginView();
                break;
            case IActivityApplicationLifetime activity:
                activity.MainViewFactory = CreateLoginView;
                break;
            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = CreateLoginView();
                break;
        }
    }

    private void BuildServices()
    {
        var config = AppBootstrap.LoadSupabaseConfig();

        var services = new ServiceCollection();
        services.AddAthloTrackCore(config);
        AppBootstrap.RegisterPlatformServices?.Invoke(services);

        services.AddTransient<LoginViewModel>();
        services.AddTransient<RecentViewModel>();
        services.AddTransient<AthletesViewModel>();
        services.AddTransient<WorkoutsViewModel>();
        services.AddTransient<CalendarViewModel>();
        services.AddTransient<AboutViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<AthleteProfileViewModelFactory>();
        services.AddSingleton<AddMeasurementViewModelFactory>();
        services.AddSingleton<AddWorkoutViewModelFactory>();
        services.AddTransient<AddAthleteViewModel>();
        services.AddSingleton<CurrentUserViewModel>();
        services.AddTransient<SetPasswordViewModel>();

        Services = services.BuildServiceProvider();
    }

    private void OnLoginSucceeded(UserRole role)
    {
        // A coach still on the password the admin issued chooses their own first.
        if (Services.GetRequiredService<SessionState>().MustSetPassword)
        {
            ShowSetPassword();
            return;
        }

        ShowMain();
    }

    private void ShowSetPassword()
    {
        var vm = Services.GetRequiredService<SetPasswordViewModel>();
        vm.Completed += ShowMain;
        vm.LoggedOut += NavigateToLogin;
        SetRoot(new SetPasswordView { DataContext = vm });
    }

    /// <summary>Replaces the app root (login / set-password screens) on every head.</summary>
    private void SetRoot(Control view)
    {
        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop when desktop.MainWindow is not null:
                desktop.MainWindow.Content = view;
                break;
            case IActivityApplicationLifetime activity:
                activity.MainViewFactory = () => view;
                break;
            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = view;
                break;
        }
    }

    private void ShowMain()
    {
        // Name + photo for the drawer header and Ρυθμίσεις.
        _ = Services.GetRequiredService<CurrentUserViewModel>().LoadAsync();

        var mainView = new MainView { DataContext = new MainViewModel() };

        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop when desktop.MainWindow is not null:
                desktop.MainWindow.Content = mainView;
                break;
            case IActivityApplicationLifetime activity:
                activity.MainViewFactory = () => new PageNavigationHost { Page = mainView };
                break;
            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = new PageNavigationHost { Page = mainView };
                break;
        }
    }
}
