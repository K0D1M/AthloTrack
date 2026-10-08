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
        // The PIN lock sits over the root: the app underneath stays as it was while locked.
        _shell.Children.Add(_root);
        _shell.Children.Add(_lockLayer);
        Services.GetRequiredService<AthloTrack.Services.PinPrompt>().Host = ShowPinPromptAsync;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new Window
            {
                Title = "AthloTrack",
                Width = 420,
                Height = 720,
                Content = _shell,
            };
            // Desktop has no background/foreground events: losing focus counts as away.
            window.Deactivated += (_, _) => OnAppBackground();
            window.Activated += (_, _) => OnAppForeground();
            desktop.MainWindow = window;
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
                return _shell;
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = _shell;
        }

        // Android and the browser: the app went to the background / came back.
        if (TryGetFeature(typeof(IActivatableLifetime)) is IActivatableLifetime activatable)
        {
            activatable.Deactivated += (_, e) => { if (e.Kind == ActivationKind.Background) OnAppBackground(); };
            activatable.Activated += (_, e) => { if (e.Kind == ActivationKind.Background) OnAppForeground(); };
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
        loginVm.LoginSucceeded += role => OnLoginSucceeded(role, loginVm.LastLoginWasRestored);
        if (_loginNotice is not null)
        {
            loginVm.ErrorMessage = _loginNotice;
            _loginNotice = null;
        }
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
        services.AddSingleton<Services.PinLock>();
        services.AddSingleton<Services.PinPrompt>();
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

    private void OnLoginSucceeded(UserRole role, bool restored)
    {
        // A coach still on the password the admin issued chooses their own first.
        if (Services.GetRequiredService<SessionState>().MustSetPassword)
        {
            ShowSetPassword();
            return;
        }

        ShowMain();
        // Opened with the saved session: the PIN unlocks it (a typed password doesn't need one).
        if (restored) ShowLockIfEnabled();
    }

    // ---- PIN lock ----

    /// <summary>The PIN keypad over everything (unlock, or a prompt from Ρυθμίσεις).</summary>
    private readonly ContentControl _lockLayer = new() { IsVisible = false };

    /// <summary>What every head shows: the root, and the lock layer above it.</summary>
    private readonly Grid _shell = new();

    private PinPadViewModel? _pad;

    /// <summary>Shown on the next login screen (e.g. after too many wrong PINs).</summary>
    private string? _loginNotice;

    /// <summary>The keypad is showing; MainView then leaves Android's back to <see cref="TryCancelPinPad"/>.</summary>
    public bool IsPinPadShowing => _lockLayer.IsVisible;

    /// <summary>Back on the keypad: cancels a Ρυθμίσεις prompt; on the lock itself it does nothing here.</summary>
    public bool TryCancelPinPad()
    {
        if (_pad is not { CanCancel: true } pad) return false;
        pad.CancelCommand.Execute(null);
        return true;
    }

    private bool IsSignedInToMain =>
        _root.Content is PageNavigationHost && Services.GetRequiredService<SessionState>().AuthUserId is not null;

    private void OnAppBackground() => Services.GetRequiredService<AthloTrack.Services.PinLock>().NoteBackground(DateTimeOffset.UtcNow);

    private void OnAppForeground()
    {
        if (Services.GetRequiredService<AthloTrack.Services.PinLock>().ShouldLockOnReturn(DateTimeOffset.UtcNow))
        {
            ShowLockIfEnabled();
        }
    }

    private void ShowLockIfEnabled()
    {
        var session = Services.GetRequiredService<SessionState>();
        var pin = Services.GetRequiredService<AthloTrack.Services.PinLock>();
        if (!IsSignedInToMain || _pad is { Mode: PinPadMode.Unlock } || !pin.IsEnabledFor(session.AuthUserId)) return;

        var pad = new PinPadViewModel(pin, PinPadMode.Unlock, session.DisplayName);
        pad.Unlocked += HidePinPad;
        pad.SignOutRequested += lockedOut => _ = SignOutFromPinAsync(lockedOut);
        ShowPinPad(pad);
    }

    /// <summary>Ρυθμίσεις: create a PIN or confirm the current one, over the app.</summary>
    private System.Threading.Tasks.Task<string?> ShowPinPromptAsync(PinPadMode mode)
    {
        var done = new System.Threading.Tasks.TaskCompletionSource<string?>();
        var pin = Services.GetRequiredService<AthloTrack.Services.PinLock>();
        var pad = new PinPadViewModel(pin, mode, Services.GetRequiredService<SessionState>().DisplayName);
        pad.Created += value => { HidePinPad(); done.TrySetResult(value); };
        pad.Unlocked += () => { HidePinPad(); done.TrySetResult("ok"); };
        pad.Cancelled += () => { HidePinPad(); done.TrySetResult(null); };
        pad.SignOutRequested += lockedOut => { done.TrySetResult(null); _ = SignOutFromPinAsync(lockedOut); };
        ShowPinPad(pad);
        return done.Task;
    }

    private void ShowPinPad(PinPadViewModel pad)
    {
        _pad = pad;
        _lockLayer.Content = new PinPadView { DataContext = pad };
        _lockLayer.IsVisible = true;
        Views.Motion.FadeInRoot(_lockLayer);
    }

    private void HidePinPad()
    {
        _lockLayer.IsVisible = false;
        _lockLayer.Content = null;
        _pad = null;
    }

    /// <summary>«Ξέχασα το PIN» or 5 wrong PINs: sign out; the password is needed again.</summary>
    private async System.Threading.Tasks.Task SignOutFromPinAsync(bool lockedOut)
    {
        Services.GetRequiredService<AthloTrack.Services.PinLock>().Clear();
        try
        {
            await Services.GetRequiredService<AthloTrack.Core.Push.PushRegistrationService>().UnregisterAsync();
            await Services.GetRequiredService<IAuthService>().SignOutAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AthloTrack] Sign-out from the PIN lock failed: {ex.Message}");
        }
        Services.GetRequiredService<SessionState>().Clear();
        HidePinPad();
        _loginNotice = lockedOut
            ? "Πολλές λάθος προσπάθειες με το PIN. Συνδέσου με email και κωδικό."
            : "Συνδέσου με email και κωδικό. Μετά μπορείς να ορίσεις νέο PIN στις Ρυθμίσεις.";
        NavigateToLogin();
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
