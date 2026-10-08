using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

/// <summary>
/// "Ορισμός κωδικού": a coach who signed in with the password the admin gave them chooses
/// their own before using the app.
/// </summary>
public partial class SetPasswordViewModel : ObservableValidator
{
    private readonly IAuthService _auth;
    private readonly ICoachRepository _coaches;
    private readonly SessionState _session;

    private readonly AthloTrack.Core.Push.PushRegistrationService _push;

    private readonly AthloTrack.Services.PinLock? _pin;

    public SetPasswordViewModel(IAuthService auth, ICoachRepository coaches, SessionState session, AthloTrack.Core.Push.PushRegistrationService push,
        AthloTrack.Services.PinLock? pin = null)
    {
        _pin = pin;
        _push = push;
        _auth = auth;
        _coaches = coaches;
        _session = session;
    }

    /// <summary>Password changed — continue into the app.</summary>
    public event Action? Completed;

    /// <summary>User chose to sign out instead.</summary>
    public event Action? LoggedOut;

    public string Greeting => $"Καλώς ήρθες, {_session.DisplayName}!";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Ο κωδικός είναι υποχρεωτικός.")]
    [MinLength(8, ErrorMessage = "Τουλάχιστον 8 χαρακτήρες.")]
    public partial string NewPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ConfirmPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        ValidateAllProperties();
        if (HasErrors)
        {
            ErrorMessage = "Παρακαλώ διορθώστε τον κωδικό.";
            return;
        }
        if (NewPassword != ConfirmPassword)
        {
            ErrorMessage = "Οι δύο κωδικοί δεν ταιριάζουν.";
            return;
        }
        if (_session.ProfileId is not { } coachId) return;

        IsBusy = true;
        try
        {
            var result = await _auth.UpdatePasswordAsync(NewPassword);
            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage?.Contains("different", StringComparison.OrdinalIgnoreCase) == true
                    ? "Ο νέος κωδικός πρέπει να διαφέρει από τον προσωρινό."
                    : result.ErrorMessage ?? "Ο κωδικός δεν άλλαξε.";
                return;
            }

            await _coaches.ClearMustSetPasswordAsync(coachId);
            _session.MustSetPassword = false;
            Completed?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        _pin?.Clear(); // a PIN never outlives its login on this device
        await _push.UnregisterAsync();
        await _auth.SignOutAsync();
        _session.Clear();
        LoggedOut?.Invoke();
    }
}
