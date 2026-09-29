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

    public SettingsViewModel(IAuthService authService, SessionState session, CurrentUserViewModel user)
    {
        _authService = authService;
        _session = session;
        User = user;
    }

    /// <summary>Raised after logout so the shell returns to the login screen.</summary>
    public event Action? LoggedOut;

    /// <summary>The signed-in user; its photo is changed from this page.</summary>
    public CurrentUserViewModel User { get; }

    [ObservableProperty]
    public partial string SelectedLanguage { get; set; } = "el";

    public string CurrentUser => _session.DisplayName ?? string.Empty;

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authService.SignOutAsync();
        _session.Clear();
        LoggedOut?.Invoke();
    }
}
