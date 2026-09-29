using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Supabase;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

public partial class LoginViewModel : ObservableValidator
{
    private readonly IAuthService _authService;
    private readonly ISessionInitializer _sessionInitializer;

    public LoginViewModel(IAuthService authService, ISessionInitializer sessionInitializer, SupabaseConfig config)
    {
        _authService = authService;
        _sessionInitializer = sessionInitializer;
        if (!AppBootstrap.IsConfigured(config))
        {
            ErrorMessage = "Δεν έχει ρυθμιστεί το Supabase. Συμπληρώστε το Assets/supabase.config.json.";
        }
    }

    /// <summary>Raised when authentication succeeds, carrying the role the user chose to log in as.</summary>
    public event Action<UserRole>? LoginSucceeded;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Το email είναι υποχρεωτικό.")]
    [EmailAddress(ErrorMessage = "Μη έγκυρο email.")]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Ο κωδικός είναι υποχρεωτικός.")]
    [MinLength(6, ErrorMessage = "Ο κωδικός πρέπει να έχει τουλάχιστον 6 χαρακτήρες.")]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>
    /// Signs back in from the stored session (the app remembers the login until the user logs
    /// out). The role isn't stored: RLS only lets a user's token see their own coaches/athletes
    /// row, so whichever profile lookup succeeds decides it. Silent on failure.
    /// </summary>
    public async Task TryRestoreSessionAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _authService.RestoreSessionAsync();
            if (!result.Success || result.UserId is null)
            {
                return;
            }

            foreach (var role in new[] { UserRole.Coach, UserRole.Athlete })
            {
                if (await _sessionInitializer.InitializeAsync(role, result.UserId.Value))
                {
                    LoginSucceeded?.Invoke(role);
                    return;
                }
            }

            // Token is valid but no profile row matches: don't keep reusing it.
            await _authService.SignOutAsync();
        }
        catch
        {
            // Offline or expired: fall back to the normal login form.
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Showing the athlete sign-up form instead of the sign-in buttons.</summary>
    [ObservableProperty]
    public partial bool IsSignUpMode { get; set; }

    /// <summary>Non-error feedback (e.g. "check your email").</summary>
    [ObservableProperty]
    public partial string? InfoMessage { get; set; }

    [RelayCommand]
    private void ToggleSignUp()
    {
        IsSignUpMode = !IsSignUpMode;
        ErrorMessage = null;
        InfoMessage = null;
    }

    /// <summary>
    /// Athlete creates their own login. The database links it to the athlete whose email the
    /// coach entered, and so to that coach.
    /// </summary>
    [RelayCommand]
    private async Task SignUpAsync()
    {
        ErrorMessage = null;
        InfoMessage = null;
        ValidateAllProperties();
        if (HasErrors)
        {
            ErrorMessage = "Παρακαλώ διορθώστε τα στοιχεία.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _authService.SignUpAsync(Email.Trim(), Password);
            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? "Η δημιουργία λογαριασμού απέτυχε.";
                return;
            }

            if (result.UserId is null)
            {
                InfoMessage = $"Στάλθηκε email επιβεβαίωσης στο {Email.Trim()}. Άνοιξε τον σύνδεσμο και μετά συνδέσου με το «Είσοδος αθλητή».";
                IsSignUpMode = false;
                return;
            }

            if (await _sessionInitializer.InitializeAsync(UserRole.Athlete, result.UserId.Value))
            {
                LoginSucceeded?.Invoke(UserRole.Athlete);
                return;
            }

            await _authService.SignOutAsync();
            InfoMessage = "Ο λογαριασμός δημιουργήθηκε, αλλά δεν αντιστοιχεί ακόμη σε αθλητή. " +
                          "Ζήτα από τον προπονητή σου να καταχωρήσει αυτό το email στο προφίλ σου και μετά συνδέσου.";
            IsSignUpMode = false;
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
    private Task LoginAsCoachAsync() => LoginAsync(UserRole.Coach);

    [RelayCommand]
    private Task LoginAsAthleteAsync() => LoginAsync(UserRole.Athlete);

    private async Task LoginAsync(UserRole role)
    {
        ErrorMessage = null;
        ValidateAllProperties();
        if (HasErrors)
        {
            ErrorMessage = "Παρακαλώ διορθώστε τα στοιχεία σύνδεσης.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _authService.SignInAsync(Email, Password);
            if (!result.Success || result.UserId is null)
            {
                ErrorMessage = result.ErrorMessage ?? "Αποτυχία σύνδεσης.";
                return;
            }

            var initialized = await _sessionInitializer.InitializeAsync(role, result.UserId.Value);
            if (!initialized)
            {
                await _authService.SignOutAsync();
                ErrorMessage = role == UserRole.Coach
                    ? "Δεν βρέθηκε προφίλ προπονητή γι' αυτόν τον λογαριασμό."
                    : "Δεν βρέθηκε προφίλ αθλητή γι' αυτόν τον λογαριασμό.";
                return;
            }

            LoginSucceeded?.Invoke(role);
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
}
