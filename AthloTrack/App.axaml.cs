using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AthloTrack.Core.Auth;
using AthloTrack.Core.DependencyInjection;
using AthloTrack.ViewModels;
using AthloTrack.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        // Surface layout/render failures (browser devtools console, Android logcat) instead of a frozen screen.
        Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
            Console.WriteLine($"[AthloTrack] Unhandled UI exception: {e.Exception}");
        UseGreekCulture();
        BuildServices();
        // Light/Dark as chosen in Ρυθμίσεις (default: follow the device), before the first view.
        ((Services.ThemeService)Services.GetRequiredService<IThemeService>()).ApplySaved();
        ConfigureCharts();

        // One permanent root on every head; screens change by swapping its content. Android
        // reads MainViewFactory only when the activity is created, so replacing the factory
        // after sign-in never showed the next screen there.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Window
            {
                Title = "AthloTrack",
                Width = 420,
                Height = 720,
                Content = _root,
            };
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime activity)
        {
            activity.MainViewFactory = () =>
            {
                // Back closes the activity but not the app; the next activity re-hosts _root. The
                // detached shell (PageNavigationHost / DrawerPage) doesn't put its pages back, which
                // left a blank screen, so the main shell is rebuilt for the new activity.
                if (_root.Content is PageNavigationHost)
                {
                    _root.Content = CreateMainShell();
                }
                return _root;
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = _root;
        }

        SetRoot(CreateLoginView());

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
    /// Gives the progress chart the bundled Inter typeface. The browser head has no system fonts,
    /// so chart legends and axis labels otherwise had nothing to render with.
    /// </summary>
    private static void ConfigureCharts()
    {
        AthloTrack.Services.ChartFonts.Register();
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
    public void NavigateToLogin() => SetRoot(CreateLoginView());

    private void BuildServices()
    {
        var config = AppBootstrap.LoadSupabaseConfig();

        var services = new ServiceCollection();
        services.AddAthloTrackCore(config);
        AppBootstrap.RegisterPlatformServices?.Invoke(services);
        // Heads without their own store still get a working login screen.
        services.TryAddSingleton<IAppPreferences, InMemoryAppPreferences>();

        services.AddSingleton<IThemeService, Services.ThemeService>();
        services.AddSingleton<Services.WorkoutAnswerSettings>();
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
        services.AddTransient<PushPromptViewModel>();
        services.AddTransient<AdminOverviewViewModel>();
        services.AddTransient<AdminAuditViewModel>();
        services.AddTransient<AdminCoachesViewModel>();
        services.AddTransient<AdminAthletesViewModel>();
        services.AddTransient<AdminPushViewModel>();

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

    /// <summary>The single root control of every head; <see cref="SetRoot"/> swaps what it shows.</summary>
    private readonly ContentControl _root = new();

    /// <summary>Shows a screen (login, set-password, main shell) on every head.</summary>
    private void SetRoot(Control view)
    {
        // On phones the shell is inset below the status bar; the root's colour fills that gap,
        // so it reads as part of the blue top bar instead of an empty strip.
        if (_root.Background is null && TryGetResource("BrandPrimaryDarkBrush", ActualThemeVariant, out var brush) && brush is Avalonia.Media.IBrush b)
        {
            _root.Background = b;
        }
        _root.Content = view;
        Views.Motion.FadeInRoot(view);
    }

    private void ShowMain()
    {
        // Name + photo for the drawer header and Ρυθμίσεις.
        _ = Services.GetRequiredService<CurrentUserViewModel>().LoadAsync();
        // Phone push (Android): link this device to the signed-in user.
        _ = Services.GetRequiredService<AthloTrack.Core.Push.PushRegistrationService>().RegisterAsync();

        SetRoot(CreateMainShell());
    }

    private static Control CreateMainShell() =>
        new PageNavigationHost { Page = new MainView { DataContext = new MainViewModel() } };
}
