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
    private readonly AthloTrack.Services.WorkoutAnswerSettings? _answers;
    private readonly AthloTrack.Services.PinLock? _pin;
    private readonly AthloTrack.Services.PinPrompt? _pinPrompt;
    private bool _syncingPin;

    public SettingsViewModel(IAuthService authService, SessionState session, CurrentUserViewModel user,
        AthloTrack.Core.Push.PushRegistrationService push, PushPromptViewModel pushPrompt, IThemeService? theme = null,
        AthloTrack.Services.WorkoutAnswerSettings? answers = null, AthloTrack.Services.PinLock? pin = null,
        AthloTrack.Services.PinPrompt? pinPrompt = null)
    {
        _pin = pin;
        _pinPrompt = pinPrompt;
        _syncingPin = true;
        PinEnabled = pin?.IsEnabledFor(session.AuthUserId) ?? false;
        _syncingPin = false;
        _push = push;
        ConfirmAnswers = answers?.ConfirmAnswers ?? true;
        _answers = answers;
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

    /// <summary>Athletes answer workouts, so only they see «Επιβεβαίωση ολοκλήρωσης».</summary>
    public bool IsAthlete => _session.Role == UserRole.Athlete;

    /// <summary>Ask «Σίγουρα;» before «Ολοκληρώθηκε» / «Δεν ολοκληρώθηκε» (this device).</summary>
    [ObservableProperty]
    public partial bool ConfirmAnswers { get; set; }

    partial void OnConfirmAnswersChanged(bool value)
    {
        if (_answers is not null) _answers.ConfirmAnswers = value;
    }

    // ---- Ασφάλεια: «Κλείδωμα με PIN» ----

    /// <summary>A 6-digit PIN unlocks the app on this device (asked on start and after a minute away).</summary>
    [ObservableProperty]
    public partial bool PinEnabled { get; set; }

    [ObservableProperty]
    public partial string? PinMessage { get; set; }

    partial void OnPinEnabledChanged(bool value)
    {
        if (_syncingPin) return;
        _ = value ? EnablePinAsync() : DisablePinAsync();
    }

    /// <summary>Turned on: choose the PIN (typed twice); cancelled leaves it off.</summary>
    private async Task EnablePinAsync()
    {
        PinMessage = null;
        var pin = await PromptAsync(ViewModels.PinPadMode.Create);
        if (pin is not null && _pin is not null && _session.AuthUserId is { } userId)
        {
            _pin.Set(userId, pin);
            PinMessage = "Το PIN ορίστηκε.";
        }
        else
        {
            SyncPin(false);
        }
    }

    /// <summary>Turned off: only with the current PIN.</summary>
    private async Task DisablePinAsync()
    {
        PinMessage = null;
        if (await PromptAsync(ViewModels.PinPadMode.ConfirmCurrent) is not null)
        {
            _pin?.Clear();
            PinMessage = "Το κλείδωμα με PIN απενεργοποιήθηκε.";
        }
        else
        {
            SyncPin(_pin?.IsEnabledFor(_session.AuthUserId) ?? false);
        }
    }

    [RelayCommand]
    private async Task ChangePinAsync()
    {
        PinMessage = null;
        if (await PromptAsync(ViewModels.PinPadMode.ConfirmCurrent) is null) return;
        var pin = await PromptAsync(ViewModels.PinPadMode.Create);
        if (pin is not null && _pin is not null && _session.AuthUserId is { } userId)
        {
            _pin.Set(userId, pin);
            PinMessage = "Το PIN άλλαξε.";
        }
        SyncPin(_pin?.IsEnabledFor(_session.AuthUserId) ?? false);
    }

    private Task<string?> PromptAsync(ViewModels.PinPadMode mode) =>
        _pinPrompt?.ShowAsync(mode) ?? Task.FromResult<string?>(null);

    private void SyncPin(bool value)
    {
        _syncingPin = true;
        PinEnabled = value;
        _syncingPin = false;
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        _pin?.Clear(); // a PIN never outlives its login on this device
        await _push.UnregisterAsync();
        await _authService.SignOutAsync();
        _session.Clear();
        LoggedOut?.Invoke();
    }
}
