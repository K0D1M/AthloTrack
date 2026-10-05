using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Supabase;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

/// <summary>Where the login screen is: picking who logs in, or the form for that choice.</summary>
public enum LoginStep
{
    ChooseRole,
    Coach,
    Athlete,
    SignUp,
}

public partial class LoginViewModel : ObservableValidator
{
    /// <summary>Preference key for the role of the last successful login on this device.</summary>
    public const string LastRoleKey = "login.last_role";

    private readonly IAuthService _authService;
    private readonly ISessionInitializer _sessionInitializer;
    private readonly IAppPreferences _preferences;

    public LoginViewModel(IAuthService authService, ISessionInitializer sessionInitializer, SupabaseConfig config,
        IAppPreferences preferences)
    {
        _authService = authService;
        _sessionInitializer = sessionInitializer;
        _preferences = preferences;

        // Back on the device that logged in before: go straight to that role's form (← still
        // leads to the choice).
        Step = preferences.Get(LastRoleKey) switch
        {
            nameof(UserRole.Coach) => LoginStep.Coach,
            nameof(UserRole.Athlete) => LoginStep.Athlete,
            _ => LoginStep.ChooseRole,
        };

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
            IsReady = true;
        }
    }

    /// <summary>
    /// The silent sign-in attempt is over and the login screen stays: the view may now put the
    /// cursor in Email (earlier, the keyboard could flash up for someone already signed in).
    /// </summary>
    [ObservableProperty]
    public partial bool IsReady { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChoosingRole), nameof(IsForm), nameof(IsSignUp), nameof(IsCoachForm),
        nameof(FormTitle), nameof(SubmitText))]
    public partial LoginStep Step { get; set; }

    public bool IsChoosingRole => Step == LoginStep.ChooseRole;
    public bool IsForm => Step != LoginStep.ChooseRole;
    public bool IsSignUp => Step == LoginStep.SignUp;

    /// <summary>The coach form uses the blue button, the athlete ones the gold.</summary>
    public bool IsCoachForm => Step == LoginStep.Coach;

    public string FormTitle => Step switch
    {
        LoginStep.Coach => "Είσοδος προπονητή",
        LoginStep.Athlete => "Είσοδος αθλητή",
        LoginStep.SignUp => "Νέος λογαριασμός αθλητή",
        _ => string.Empty,
    };

    public string SubmitText => Step == LoginStep.SignUp ? "Δημιουργία λογαριασμού" : "Είσοδος";

    /// <summary>Non-error feedback (e.g. "check your email").</summary>
    [ObservableProperty]
    public partial string? InfoMessage { get; set; }

    [RelayCommand]
    private void ChooseCoach() => GoTo(LoginStep.Coach);

    [RelayCommand]
    private void ChooseAthlete() => GoTo(LoginStep.Athlete);

    [RelayCommand]
    private void ChooseSignUp() => GoTo(LoginStep.SignUp);

    [RelayCommand]
    private void Back() => GoTo(LoginStep.ChooseRole);

    /// <summary>The form's single button: sign in as the chosen role, or create the account.</summary>
    [RelayCommand]
    private Task SubmitAsync() => Step switch
    {
        LoginStep.Coach => LoginAsync(UserRole.Coach),
        LoginStep.Athlete => LoginAsync(UserRole.Athlete),
        LoginStep.SignUp => SignUpAsync(),
        _ => Task.CompletedTask,
    };

    private void GoTo(LoginStep step)
    {
        if (IsBusy) return;
        Step = step;
        ErrorMessage = null;
        InfoMessage = null;
        // Don't greet the next form with the previous one's validation errors.
        ClearErrors();
    }

    private void RememberRole(UserRole role) => _preferences.Set(LastRoleKey, role.ToString());

    /// <summary>
    /// Athlete creates their own login. The database links it to the athlete whose email the
    /// coach entered, and so to that coach.
    /// </summary>
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
                InfoMessage = $"Στάλθηκε email επιβεβαίωσης στο {Email.Trim()}. Άνοιξε τον σύνδεσμο και μετά συνδέσου εδώ.";
                Step = LoginStep.Athlete;
                RememberRole(UserRole.Athlete);
                return;
            }

            if (await _sessionInitializer.InitializeAsync(UserRole.Athlete, result.UserId.Value))
            {
                RememberRole(UserRole.Athlete);
                LoginSucceeded?.Invoke(UserRole.Athlete);
                return;
            }

            await _authService.SignOutAsync();
            InfoMessage = "Ο λογαριασμός δημιουργήθηκε, αλλά δεν αντιστοιχεί ακόμη σε αθλητή. " +
                          "Ζήτα από τον προπονητή σου να καταχωρήσει αυτό το email στο προφίλ σου και μετά συνδέσου.";
            Step = LoginStep.Athlete;
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

            RememberRole(role);
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
