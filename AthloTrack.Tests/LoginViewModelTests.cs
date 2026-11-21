using AthloTrack.Core.Auth;
using AthloTrack.Core.Supabase;
using AthloTrack.ViewModels;

namespace AthloTrack.Tests;

public class LoginViewModelTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class FakeAuth : IAuthService
    {
        public bool SignedOut { get; private set; }
        public AuthResult SignUpResult { get; set; } = AuthResult.Ok(UserId, "a", "r");

        public Task<AuthResult> SignInAsync(string email, string password) =>
            Task.FromResult(password == "secret1" ? AuthResult.Ok(UserId, "a", "r") : AuthResult.Fail("wrong"));

        public AuthResult RestoreResult { get; set; } = AuthResult.Fail("none");
        public Task<AuthResult> RestoreSessionAsync() => Task.FromResult(RestoreResult);
        public Task<AuthResult> SignUpAsync(string email, string password) => Task.FromResult(SignUpResult);
        public Task<AuthResult> UpdatePasswordAsync(string newPassword) => Task.FromResult(AuthResult.Fail("n/a"));

        public Task SignOutAsync()
        {
            SignedOut = true;
            return Task.CompletedTask;
        }
    }

    /// <summary>The account only has a profile for <see cref="Role"/> (null: none yet).</summary>
    private sealed class FakeInitializer(UserRole? role) : ISessionInitializer
    {
        public UserRole? Role { get; set; } = role;
        public Task<bool> InitializeAsync(UserRole r, Guid authUserId) => Task.FromResult(r == Role);
    }

    /// <summary>The «email» function: code 123456 is right.</summary>
    private sealed class FakeAccounts : IAccountService
    {
        public List<string> ResetCodesSentTo { get; } = [];
        public int LinkCodesSent { get; private set; }
        public bool ProfileWaits { get; set; } = true;
        public FakeInitializer? LinksTo { get; set; }
        public string? NewPassword { get; private set; }

        public Task<AuthResult> SendResetCodeAsync(string email)
        {
            ResetCodesSentTo.Add(email);
            return Task.FromResult(new AuthResult(true, null, null, null, null));
        }

        public Task<AuthResult> ResetPasswordAsync(string email, string code, string newPassword)
        {
            if (code != "123456") return Task.FromResult(AuthResult.Fail(AccountService.WrongResetCode));
            NewPassword = newPassword;
            return Task.FromResult(AuthResult.Ok(UserId, "a", "r"));
        }

        public Task<LinkCodeResult> SendLinkCodeAsync()
        {
            LinkCodesSent++;
            return Task.FromResult(new LinkCodeResult(ProfileWaits, ProfileWaits ? "Νίκος Αλεξίου" : null));
        }

        public Task<LinkVerifyResult> VerifyLinkCodeAsync(string code)
        {
            if (code != "123456") return Task.FromResult(new LinkVerifyResult(false, "Λάθος κωδικός. Απομένουν 4 προσπάθειες.", 4));
            if (LinksTo is not null) LinksTo.Role = UserRole.Athlete;
            return Task.FromResult(new LinkVerifyResult(true));
        }
    }

    private static readonly SupabaseConfig Config = new() { Url = "https://x.supabase.co", AnonKey = "key" };

    private static LoginViewModel Create(IAppPreferences prefs, UserRole? profile = UserRole.Coach, FakeAuth? auth = null,
        FakeAccounts? accounts = null, FakeInitializer? initializer = null) =>
        new(auth ?? new FakeAuth(), initializer ?? new FakeInitializer(profile), Config, prefs, accounts) { ResendWaitSeconds = 0 };

    private static async Task SignIn(LoginViewModel vm, string password = "secret1")
    {
        vm.Email = "someone@example.com";
        vm.Password = password;
        await vm.SubmitCommand.ExecuteAsync(null);
    }

    [Fact]
    public void Opens_on_sign_in_with_email_and_password()
    {
        var vm = Create(new InMemoryAppPreferences());

        Assert.Equal(LoginStep.SignIn, vm.Step);
        Assert.True(vm.ShowsEmail);
        Assert.True(vm.ShowsPassword);
        Assert.False(vm.HasBack);
        Assert.Equal("Σύνδεση", vm.SubmitText);
        Assert.True(vm.CanSignUp);
    }

    [Theory]
    [InlineData(UserRole.Coach)]
    [InlineData(UserRole.Athlete)]
    [InlineData(UserRole.Admin)]
    public async Task One_form_finds_the_role_and_remembers_it(UserRole profile)
    {
        var prefs = new InMemoryAppPreferences();
        var auth = new FakeAuth();
        var vm = Create(prefs, profile, auth);
        UserRole? loggedIn = null;
        vm.LoginSucceeded += r => loggedIn = r;

        await SignIn(vm);

        Assert.Equal(profile, loggedIn);
        Assert.False(auth.SignedOut);
        Assert.Null(vm.ErrorMessage);
        Assert.Equal(profile.ToString(), prefs.Get(LoginViewModel.LastRoleKey));
    }

    [Fact]
    public async Task A_wrong_password_shows_the_error()
    {
        var vm = Create(new InMemoryAppPreferences());
        var signedIn = false;
        vm.LoginSucceeded += _ => signedIn = true;

        await SignIn(vm, "wrong-pass");

        Assert.False(signedIn);
        Assert.Equal("wrong", vm.ErrorMessage);
    }

    [Fact]
    public async Task No_profile_and_no_waiting_athlete_signs_out()
    {
        var prefs = new InMemoryAppPreferences();
        var auth = new FakeAuth();
        var accounts = new FakeAccounts { ProfileWaits = false };
        var vm = Create(prefs, profile: null, auth, accounts);

        await SignIn(vm);

        Assert.True(auth.SignedOut);
        Assert.Equal(LoginStep.SignIn, vm.Step);
        Assert.StartsWith("Δεν βρέθηκε προφίλ", vm.ErrorMessage);
        Assert.Null(prefs.Get(LoginViewModel.LastRoleKey));
    }

    [Fact]
    public async Task Changing_card_clears_the_previous_errors()
    {
        var vm = Create(new InMemoryAppPreferences(), accounts: new FakeAccounts());

        await vm.SubmitCommand.ExecuteAsync(null); // empty fields
        Assert.NotNull(vm.ErrorMessage);
        Assert.True(vm.HasErrors);

        vm.ShowSignUpCommand.Execute(null);
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.HasErrors);
        Assert.True(vm.HasBack);
        vm.BackCommand.Execute(null);
        Assert.Equal(LoginStep.SignIn, vm.Step);
    }

    [Fact]
    public async Task Sign_up_needing_email_confirmation_lands_on_sign_in()
    {
        var auth = new FakeAuth { SignUpResult = new AuthResult(true, null, null, null, null) };
        var vm = Create(new InMemoryAppPreferences(), UserRole.Athlete, auth);

        vm.ShowSignUpCommand.Execute(null);
        await SignIn(vm);

        Assert.Equal(LoginStep.SignIn, vm.Step);
        Assert.NotNull(vm.InfoMessage);
    }

    [Fact]
    public async Task A_restored_session_is_marked_for_the_pin_lock_and_a_typed_password_is_not()
    {
        var auth = new FakeAuth { RestoreResult = AuthResult.Ok(UserId, "a", "r") };
        var restored = Create(new InMemoryAppPreferences(), UserRole.Coach, auth);
        await restored.TryRestoreSessionAsync();
        Assert.True(restored.LastLoginWasRestored);

        var typed = Create(new InMemoryAppPreferences(), UserRole.Coach);
        await SignIn(typed);
        Assert.False(typed.LastLoginWasRestored);
    }

    // ---- «Ξέχασα τον κωδικό» ----

    [Fact]
    public async Task Forgot_password_sends_a_code_then_the_code_and_new_password_sign_in()
    {
        var accounts = new FakeAccounts();
        var vm = Create(new InMemoryAppPreferences(), UserRole.Athlete, accounts: accounts);
        UserRole? loggedIn = null;
        vm.LoginSucceeded += r => loggedIn = r;

        vm.ShowForgotCommand.Execute(null);
        Assert.True(vm.IsForgot);
        Assert.False(vm.ShowsPassword);
        vm.Email = "maria@example.com";
        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.Equal(["maria@example.com"], accounts.ResetCodesSentTo);
        Assert.True(vm.IsResetCode);
        Assert.Contains("στάλθηκε κωδικός", vm.InfoMessage);

        vm.Code = "481 927"; // wrong
        vm.NewPassword = "newpass1";
        vm.NewPasswordAgain = "newpass1";
        await vm.SubmitCommand.ExecuteAsync(null);
        Assert.Equal(AccountService.WrongResetCode, vm.ErrorMessage);
        Assert.Null(loggedIn);

        vm.Code = "123 456";
        await vm.SubmitCommand.ExecuteAsync(null);
        Assert.Equal("newpass1", accounts.NewPassword);
        Assert.Equal(UserRole.Athlete, loggedIn); // straight in
    }

    [Theory]
    [InlineData("12345", "newpass1", "newpass1", "Γράψε τον 6ψήφιο κωδικό από το email.")]
    [InlineData("123456", "abc", "abc", "Ο νέος κωδικός πρέπει να έχει τουλάχιστον 6 χαρακτήρες.")]
    [InlineData("123456", "newpass1", "newpass2", "Οι δύο κωδικοί δεν είναι ίδιοι.")]
    public async Task The_new_password_card_checks_its_fields(string code, string password, string again, string error)
    {
        var accounts = new FakeAccounts();
        var vm = Create(new InMemoryAppPreferences(), accounts: accounts);
        vm.ShowForgotCommand.Execute(null);
        vm.Email = "maria@example.com";
        await vm.SubmitCommand.ExecuteAsync(null);

        vm.Code = code;
        vm.NewPassword = password;
        vm.NewPasswordAgain = again;
        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.Equal(error, vm.ErrorMessage);
        Assert.Null(accounts.NewPassword);
    }

    [Fact]
    public async Task Forgot_password_needs_a_valid_email_and_can_resend()
    {
        var accounts = new FakeAccounts();
        var vm = Create(new InMemoryAppPreferences(), accounts: accounts);
        vm.ShowForgotCommand.Execute(null);

        vm.Email = "not-an-email";
        await vm.SubmitCommand.ExecuteAsync(null);
        Assert.Empty(accounts.ResetCodesSentTo);
        Assert.Equal("Γράψε ένα έγκυρο email.", vm.ErrorMessage);

        vm.Email = "maria@example.com";
        await vm.SubmitCommand.ExecuteAsync(null);
        Assert.True(vm.CanResend); // no wait in tests
        await vm.ResendCommand.ExecuteAsync(null);
        Assert.Equal(2, accounts.ResetCodesSentTo.Count);

        vm.BackCommand.Execute(null);
        Assert.True(vm.IsForgot);
    }

    [Fact]
    public void Without_the_email_service_there_is_no_reset()
    {
        Assert.False(Create(new InMemoryAppPreferences()).CanResetPassword);
        Assert.True(Create(new InMemoryAppPreferences(), accounts: new FakeAccounts()).CanResetPassword);
    }

    // ---- «Επιβεβαίωσε το email σου» ----

    [Fact]
    public async Task An_athlete_login_with_a_waiting_profile_links_with_the_code()
    {
        var initializer = new FakeInitializer(null);
        var accounts = new FakeAccounts { LinksTo = initializer };
        var auth = new FakeAuth();
        var vm = Create(new InMemoryAppPreferences(), auth: auth, accounts: accounts, initializer: initializer);
        UserRole? loggedIn = null;
        vm.LoginSucceeded += r => loggedIn = r;

        vm.ShowSignUpCommand.Execute(null);
        await SignIn(vm);

        Assert.True(vm.IsVerifyLink);
        Assert.False(auth.SignedOut); // stays signed in for the code
        Assert.Equal(1, accounts.LinkCodesSent);
        Assert.Contains("Νίκος Αλεξίου", vm.LinkIntro);
        Assert.True(vm.IsGoldButton);

        vm.Code = "000000";
        await vm.SubmitCommand.ExecuteAsync(null);
        Assert.Equal("Λάθος κωδικός. Απομένουν 4 προσπάθειες.", vm.ErrorMessage);
        Assert.Null(loggedIn);

        vm.Code = "123456";
        await vm.SubmitCommand.ExecuteAsync(null);
        Assert.Equal(UserRole.Athlete, loggedIn);
    }

    [Fact]
    public async Task Signing_out_of_the_link_code_goes_back_to_sign_in()
    {
        var auth = new FakeAuth();
        var vm = Create(new InMemoryAppPreferences(), profile: null, auth, new FakeAccounts());
        await SignIn(vm);
        Assert.True(vm.IsVerifyLink);

        await vm.CancelLinkCommand.ExecuteAsync(null);

        Assert.True(auth.SignedOut);
        Assert.True(vm.IsSignIn);
    }

    [Fact]
    public async Task A_new_login_without_a_profile_is_told_to_ask_the_coach()
    {
        var auth = new FakeAuth();
        var vm = Create(new InMemoryAppPreferences(), profile: null, auth, new FakeAccounts { ProfileWaits = false });

        vm.ShowSignUpCommand.Execute(null);
        await SignIn(vm);

        Assert.True(auth.SignedOut);
        Assert.True(vm.IsSignIn);
        Assert.StartsWith("Ο λογαριασμός δημιουργήθηκε", vm.InfoMessage);
    }
}
