using System;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

/// <summary>Ρυθμίσεις — own photo, language, logout.</summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly SessionState _session;

    private readonly AthloTrack.Core.Push.PushRegistrationService _push;
    private readonly IThemeService? _theme;

    public SettingsViewModel(IAuthService authService, SessionState session, CurrentUserViewModel user,
        AthloTrack.Core.Push.PushRegistrationService push, PushPromptViewModel pushPrompt, IThemeService? theme = null)
    {
        _push = push;
        // Show the saved choice first, then listen: opening the page mustn't re-apply it.
        Theme = theme?.Current ?? ThemeChoice.System;
        _theme = theme;
        _authService = authService;
        _session = session;
        User = user;
        Push = pushPrompt;
    }

    /// <summary>Web: turning phone/browser notifications on for this device.</summary>
    public PushPromptViewModel Push { get; }

    /// <summary>Raised after logout so the shell returns to the login screen.</summary>
    public event Action? LoggedOut;

    /// <summary>The signed-in user; its photo is changed from this page.</summary>
    public CurrentUserViewModel User { get; }

    [ObservableProperty]
    public partial string SelectedLanguage { get; set; } = "el";

    /// <summary>Θέμα: Σύστημα / Ανοιχτό / Σκούρο, applied at once and remembered on this device.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSystemTheme), nameof(IsLightTheme), nameof(IsDarkTheme))]
    public partial ThemeChoice Theme { get; set; }

    partial void OnThemeChanged(ThemeChoice value) => _theme?.Set(value);

    // The three radio buttons; only the one being checked sets the choice.
    public bool IsSystemTheme { get => Theme == ThemeChoice.System; set { if (value) Theme = ThemeChoice.System; } }
    public bool IsLightTheme { get => Theme == ThemeChoice.Light; set { if (value) Theme = ThemeChoice.Light; } }
    public bool IsDarkTheme { get => Theme == ThemeChoice.Dark; set { if (value) Theme = ThemeChoice.Dark; } }

    public string CurrentUser => _session.DisplayName ?? string.Empty;

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _push.UnregisterAsync();
        await _authService.SignOutAsync();
        _session.Clear();
        LoggedOut?.Invoke();
    }
}
