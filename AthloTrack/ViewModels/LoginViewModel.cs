using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Supabase;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

/// <summary>What the login card shows.</summary>
public enum LoginStep
{
    /// <summary>Email and password: the app finds the role (coach, athlete, admin) itself.</summary>
    SignIn,
    /// <summary>A new athlete login.</summary>
    SignUp,
    /// <summary>«Ξέχασα τον κωδικό»: the email to send the code to.</summary>
    Forgot,
    /// <summary>The emailed code and the new password.</summary>
    ResetCode,
    /// <summary>«Επιβεβαίωσε το email σου»: the code that links the login to its athlete profile.</summary>
    VerifyLink,
}

public partial class LoginViewModel : ObservableValidator
{
    /// <summary>Preference key for the role of the last successful login on this device.</summary>
    public const string LastRoleKey = "login.last_role";

    private readonly IAuthService _authService;
    private readonly ISessionInitializer _sessionInitializer;
    private readonly IAppPreferences _preferences;
    private readonly IAccountService? _accounts;

    public LoginViewModel(IAuthService authService, ISessionInitializer sessionInitializer, SupabaseConfig config,
        IAppPreferences preferences, IAccountService? accounts = null)
    {
        _authService = authService;
        _sessionInitializer = sessionInitializer;
        _preferences = preferences;
        _accounts = accounts;

        if (!AppBootstrap.IsConfigured(config))
        {
            ErrorMessage = "Δεν έχει ρυθμιστεί το Supabase. Συμπληρώστε το Assets/supabase.config.json.";
        }
    }

    /// <summary>Raised when authentication succeeds, with the role the login turned out to have.</summary>
    public event Action<UserRole>? LoginSucceeded;

    /// <summary>The roles a login is looked up as, in this order. An administrator is tried first.</summary>
    public IReadOnlyList<UserRole> SignInRoles { get; set; } = [UserRole.Admin, UserRole.Coach, UserRole.Athlete];

    /// <summary>The message when the login has none of <see cref="SignInRoles"/>.</summary>
    public string NoProfileMessage { get; set; } =
        "Δεν βρέθηκε προφίλ γι' αυτόν τον λογαριασμό. Αν είσαι αθλητής, ζήτα από τον προπονητή σου να καταχωρήσει αυτό το email.";

    /// <summary>Athletes may sign in here, so they may also create a login.</summary>
    public bool CanSignUp => SignInRoles.Contains(UserRole.Athlete);

    /// <summary>«Ξέχασα τον κωδικό» needs the email service.</summary>
    public bool CanResetPassword => _accounts is not null;

    // ---- fields ----

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

    /// <summary>The 6-digit code from the email (reset or link).</summary>
    [ObservableProperty]
    public partial string Code { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewPasswordAgain { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>Non-error feedback (e.g. «Στάλθηκε κωδικός»).</summary>
    [ObservableProperty]
    public partial string? InfoMessage { get; set; }

    /// <summary>The athlete's coach while linking: «Ο προπονητής σου, Νίκος Αλεξίου, …».</summary>
    [ObservableProperty]
    public partial string? LinkCoach { get; set; }

    public string LinkIntro => string.IsNullOrWhiteSpace(LinkCoach)
        ? "Υπάρχει προφίλ αθλητή με αυτό το email. Για να συνδεθεί ο λογαριασμός σου μαζί του, γράψε τον κωδικό που στείλαμε στο"
        : $"Ο προπονητής σου, {LinkCoach}, έχει προφίλ για σένα με αυτό το email. Για να συνδεθεί ο λογαριασμός σου μαζί του, γράψε τον κωδικό που στείλαμε στο";

    partial void OnLinkCoachChanged(string? value) => OnPropertyChanged(nameof(LinkIntro));

    // ---- steps ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignIn), nameof(IsSignUp), nameof(IsForgot), nameof(IsResetCode), nameof(IsVerifyLink),
        nameof(FormTitle), nameof(SubmitText), nameof(IsGoldButton), nameof(HasBack), nameof(IsCodeStep), nameof(ShowsPassword))]
    public partial LoginStep Step { get; set; }

    public bool IsSignIn => Step == LoginStep.SignIn;
    public bool IsSignUp => Step == LoginStep.SignUp;
    public bool IsForgot => Step == LoginStep.Forgot;
    public bool IsResetCode => Step == LoginStep.ResetCode;
    public bool IsVerifyLink => Step == LoginStep.VerifyLink;
    public bool IsCodeStep => Step is LoginStep.ResetCode or LoginStep.VerifyLink;

    /// <summary>The Email box shows for sign-in, sign-up and «Ξέχασα τον κωδικό».</summary>
    public bool ShowsEmail => Step is LoginStep.SignIn or LoginStep.SignUp or LoginStep.Forgot;

    /// <summary>The Κωδικός box shows for sign-in and sign-up.</summary>
    public bool ShowsPassword => Step is LoginStep.SignIn or LoginStep.SignUp;

    /// <summary>← in the card's title: every step but the first and the link code.</summary>
    public bool HasBack => Step is LoginStep.SignUp or LoginStep.Forgot or LoginStep.ResetCode;

    /// <summary>The gold button (athlete actions); the others are blue.</summary>
    public bool IsGoldButton => Step is LoginStep.SignUp or LoginStep.VerifyLink;

    public string FormTitle => Step switch
    {
        LoginStep.SignUp => "Νέος λογαριασμός αθλητή",
        LoginStep.Forgot => "Ξέχασα τον κωδικό",
        LoginStep.ResetCode => "Νέος κωδικός",
        LoginStep.VerifyLink => "Επιβεβαίωσε το email σου",
        _ => "Σύνδεση",
    };

    public string SubmitText => Step switch
    {
        LoginStep.SignUp => "Δημιουργία λογαριασμού",
        LoginStep.Forgot => "Αποστολή κωδικού",
        LoginStep.ResetCode => "Αλλαγή κωδικού",
        LoginStep.VerifyLink => "Επιβεβαίωση",
        _ => "Σύνδεση",
    };

    partial void OnStepChanged(LoginStep value) => OnPropertyChanged(nameof(ShowsEmail));

    [RelayCommand]
    private void ShowSignUp() => GoTo(LoginStep.SignUp);

    [RelayCommand]
    private void ShowSignIn() => GoTo(LoginStep.SignIn);

    [RelayCommand]
    private void ShowForgot() => GoTo(LoginStep.Forgot);

    /// <summary>←: the code step goes back to its email; the rest to sign-in.</summary>
    [RelayCommand]
    private void Back() => GoTo(Step == LoginStep.ResetCode ? LoginStep.Forgot : LoginStep.SignIn);

    /// <summary>A link or ← tapped: ignored while something is running.</summary>
    private void GoTo(LoginStep step)
    {
        if (IsBusy) return;
        Show(step);
    }

    private void Show(LoginStep step)
    {
        Step = step;
        ErrorMessage = null;
        InfoMessage = null;
        Code = string.Empty;
        // Don't greet the next card with the previous one's validation errors.
        ClearErrors();
    }

    /// <summary>The card's single button.</summary>
    [RelayCommand]
    private Task SubmitAsync() => Step switch
    {
        LoginStep.SignUp => SignUpAsync(),
        LoginStep.Forgot => SendResetCodeAsync(),
        LoginStep.ResetCode => ResetPasswordAsync(),
        LoginStep.VerifyLink => VerifyLinkAsync(),
        _ => SignInAsync(),
    };

    // ---- «Στείλε ξανά» ----

    /// <summary>Seconds before a code may be sent again (the function allows one a minute).</summary>
    public int ResendWaitSeconds { get; set; } = 60;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanResend), nameof(ResendText))]
    public partial int ResendIn { get; set; }

    public bool CanResend => ResendIn <= 0;
    public string ResendText => CanResend ? "Στείλε ξανά" : $"Στείλε ξανά σε {ResendIn / 60}:{ResendIn % 60:00}";

    private int _countdown;

    private async void StartResendCountdown()
    {
        var run = ++_countdown;
        ResendIn = ResendWaitSeconds;
        while (ResendIn > 0 && run == _countdown)
        {
            await Task.Delay(1000);
            if (run == _countdown) ResendIn--;
        }
    }

    [RelayCommand]
    private Task ResendAsync()
    {
        if (!CanResend || IsBusy) return Task.CompletedTask;
        return Step == LoginStep.VerifyLink ? SendLinkCodeAsync() : SendResetCodeAsync();
    }

    // ---- the saved sign-in ----

    /// <summary>The sign-in came from the saved session, not a typed password: the PIN lock applies.</summary>
    public bool LastLoginWasRestored { get; private set; }

    /// <summary>
    /// How long the saved sign-in may take before the login form is given back: on a slow or
    /// stuck connection each request could otherwise wait 100 s, with the form disabled.
    /// </summary>
    public TimeSpan RestoreTimeout { get; set; } = TimeSpan.FromSeconds(8);

    /// <summary>The restore ran past <see cref="RestoreTimeout"/>: its late result is ignored.</summary>
    private bool _restoreAbandoned;

    /// <summary>
    /// The silent sign-in attempt is over and the login screen stays: the view may now put the
    /// cursor in Email (earlier, the keyboard could flash up for someone already signed in).
    /// </summary>
    [ObservableProperty]
    public partial bool IsReady { get; set; }

    /// <summary>
    /// Signs back in from the stored session (the app remembers the login until the user logs
    /// out). The role isn't stored: RLS only lets a user's token see their own admins/coaches/
    /// athletes row, so whichever profile lookup succeeds decides it. Silent on failure, and
    /// after <see cref="RestoreTimeout"/> the form is usable whatever the network does.
    /// </summary>
    public async Task TryRestoreSessionAsync()
    {
        IsBusy = true;
        try
        {
            var restore = RestoreAsync();
            if (await Task.WhenAny(restore, Task.Delay(RestoreTimeout)) != restore)
            {
                _restoreAbandoned = true;
                Console.WriteLine("[AthloTrack] saved sign-in timed out: showing the login form");
                return;
            }

            if (await restore is { } role)
            {
                LastLoginWasRestored = true; // the app may ask for the PIN (PinLock)
                LoginSucceeded?.Invoke(role);
            }
        }
        finally
        {
            IsBusy = false;
            IsReady = true;
        }
    }

    /// <summary>The restored session's role, or null (no session, no profile, offline).</summary>
    private async Task<UserRole?> RestoreAsync()
    {
        try
        {
            var result = await _authService.RestoreSessionAsync();
            if (!result.Success || result.UserId is null)
            {
                return null;
            }

            if (await FindRoleAsync(result.UserId.Value) is { } role)
            {
                return role;
            }

            // Token is valid but no profile row matches: don't keep reusing it (unless the form
            // took over meanwhile and the user may be signing in).
            if (!_restoreAbandoned) await _authService.SignOutAsync();
            return null;
        }
        catch
        {
            // Offline or expired: fall back to the normal login form.
            return null;
        }
    }

    /// <summary>The first of <see cref="SignInRoles"/> with a profile for this login, or null.</summary>
    private async Task<UserRole?> FindRoleAsync(Guid userId)
    {
        foreach (var role in SignInRoles)
        {
            bool found;
            try
            {
                found = await _sessionInitializer.InitializeAsync(role, userId);
            }
            catch when (role == UserRole.Admin && SignInRoles.Count > 1)
            {
                found = false; // the admin lookup failing must not stop coaches and athletes
            }

            if (found) return role;
        }
        return null;
    }

    private void Succeed(UserRole role)
    {
        _preferences.Set(LastRoleKey, role.ToString());
        LoginSucceeded?.Invoke(role);
    }

    private bool ValidateEmailAndPassword(string error)
    {
        ErrorMessage = null;
        InfoMessage = null;
        ValidateAllProperties();
        if (!HasErrors) return true;
        ErrorMessage = error;
        return false;
    }

    // ---- sign in / sign up ----

    private async Task SignInAsync()
    {
        if (!ValidateEmailAndPassword("Παρακαλώ διορθώστε τα στοιχεία σύνδεσης.")) return;

        IsBusy = true;
        try
        {
            var result = await _authService.SignInAsync(Email.Trim(), Password);
            if (!result.Success || result.UserId is null)
            {
                ErrorMessage = result.ErrorMessage ?? "Αποτυχία σύνδεσης.";
                return;
            }

            if (await FindRoleAsync(result.UserId.Value) is { } role)
            {
                Succeed(role);
                return;
            }

            await NoProfileAsync(result.UserId.Value, NoProfileMessage);
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

    /// <summary>
    /// Athlete creates their own login. It links to the athlete profile with that email only after
    /// the code sent to the email (no automatic linking: 022_email_and_linking.sql).
    /// </summary>
    private async Task SignUpAsync()
    {
        if (!ValidateEmailAndPassword("Παρακαλώ διορθώστε τα στοιχεία.")) return;

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
                Step = LoginStep.SignIn;
                return;
            }

            if (await FindRoleAsync(result.UserId.Value) is { } role)
            {
                Succeed(role);
                return;
            }

            await NoProfileAsync(result.UserId.Value, "Ο λογαριασμός δημιουργήθηκε, αλλά δεν αντιστοιχεί ακόμη σε αθλητή. " +
                                 "Ζήτα από τον προπονητή σου να καταχωρήσει αυτό το email στο προφίλ σου και μετά συνδέσου.",
                info: true);
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

    /// <summary>
    /// Signed in, but no profile: an athlete profile with this email may wait for the link code.
    /// Otherwise sign out with <paramref name="message"/>.
    /// </summary>
    private async Task NoProfileAsync(Guid userId, string message, bool info = false)
    {
        if (CanSignUp && _accounts is not null)
        {
            var link = await _accounts.SendLinkCodeAsync();
            if (link.Match)
            {
                LinkCoach = link.Coach;
                _linkUserId = userId;
                Show(LoginStep.VerifyLink);
                StartResendCountdown();
                return;
            }
        }

        await _authService.SignOutAsync();
        Step = LoginStep.SignIn;
        if (info) InfoMessage = message;
        else ErrorMessage = message;
    }

    // ---- «Ξέχασα τον κωδικό» ----

    private async Task SendResetCodeAsync()
    {
        ErrorMessage = null;
        InfoMessage = null;
        ValidateProperty(Email, nameof(Email));
        if (GetErrors(nameof(Email)).Cast<object>().Any())
        {
            ErrorMessage = "Γράψε ένα έγκυρο email.";
            return;
        }
        if (_accounts is null) return;

        IsBusy = true;
        try
        {
            var result = await _accounts.SendResetCodeAsync(Email.Trim());
            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            if (Step != LoginStep.ResetCode)
            {
                Show(LoginStep.ResetCode);
            }
            InfoMessage = $"Αν υπάρχει λογαριασμός με το {Email.Trim()}, στάλθηκε κωδικός. Έλεγξε και τα ανεπιθύμητα.";
            StartResendCountdown();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ResetPasswordAsync()
    {
        ErrorMessage = null;
        // Supabase's recovery code: 6 to 8 digits, as the project's «Email OTP length» says.
        var code = AccountService.Digits(Code);
        if (code.Length is < 6 or > 8)
        {
            ErrorMessage = "Γράψε τον κωδικό από το email.";
            return;
        }
        if (NewPassword.Length < 6)
        {
            ErrorMessage = "Ο νέος κωδικός πρέπει να έχει τουλάχιστον 6 χαρακτήρες.";
            return;
        }
        if (NewPassword != NewPasswordAgain)
        {
            ErrorMessage = "Οι δύο κωδικοί δεν είναι ίδιοι.";
            return;
        }
        if (_accounts is null) return;

        IsBusy = true;
        try
        {
            var result = await _accounts.ResetPasswordAsync(Email.Trim(), code, NewPassword);
            if (!result.Success || result.UserId is null)
            {
                ErrorMessage = result.ErrorMessage ?? AccountService.WrongResetCode;
                return;
            }

            // Signed in with the new password: straight into the app.
            if (await FindRoleAsync(result.UserId.Value) is { } role)
            {
                Succeed(role);
                return;
            }
            await NoProfileAsync(result.UserId.Value, NoProfileMessage);
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

    // ---- «Επιβεβαίωσε το email σου» ----

    /// <summary>The signed-in login waiting for its link code.</summary>
    private Guid? _linkUserId;

    private async Task SendLinkCodeAsync()
    {
        if (_accounts is null) return;
        IsBusy = true;
        try
        {
            ErrorMessage = null;
            var link = await _accounts.SendLinkCodeAsync();
            if (link.Error is not null) ErrorMessage = link.Error;
            else
            {
                InfoMessage = "Στάλθηκε νέος κωδικός.";
                StartResendCountdown();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task VerifyLinkAsync()
    {
        ErrorMessage = null;
        InfoMessage = null;
        var code = AccountService.Digits(Code);
        if (code.Length != 6)
        {
            ErrorMessage = "Γράψε τον 6ψήφιο κωδικό από το email.";
            return;
        }
        if (_accounts is null) return;

        IsBusy = true;
        try
        {
            var result = await _accounts.VerifyLinkCodeAsync(code);
            if (!result.Linked)
            {
                ErrorMessage = result.Error ?? "Λάθος κωδικός.";
                return;
            }

            if (_linkUserId is { } userId && await _sessionInitializer.InitializeAsync(UserRole.Athlete, userId))
            {
                Succeed(UserRole.Athlete);
                return;
            }
            ErrorMessage = "Ο λογαριασμός συνδέθηκε. Συνδέσου ξανά για να συνεχίσεις.";
            await _authService.SignOutAsync();
            Step = LoginStep.SignIn;
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

    /// <summary>«Αποσύνδεση» on the link code: another login, or later.</summary>
    [RelayCommand]
    private async Task CancelLinkAsync()
    {
        if (IsBusy) return;
        await _authService.SignOutAsync();
        _linkUserId = null;
        Show(LoginStep.SignIn);
    }
}
