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

        public Task<AuthResult> RestoreSessionAsync() => Task.FromResult(AuthResult.Fail("none"));
        public Task<AuthResult> SignUpAsync(string email, string password) => Task.FromResult(SignUpResult);
        public Task<AuthResult> UpdatePasswordAsync(string newPassword) => Task.FromResult(AuthResult.Fail("n/a"));

        public Task SignOutAsync()
        {
            SignedOut = true;
            return Task.CompletedTask;
        }
    }

    /// <summary>The account only has a profile for <see cref="Role"/>.</summary>
    private sealed class FakeInitializer(UserRole role) : ISessionInitializer
    {
        public Task<bool> InitializeAsync(UserRole r, Guid authUserId) => Task.FromResult(r == role);
    }

    private static readonly SupabaseConfig Config = new() { Url = "https://x.supabase.co", AnonKey = "key" };

    private static LoginViewModel Create(IAppPreferences prefs, UserRole profile = UserRole.Coach, FakeAuth? auth = null) =>
        new(auth ?? new FakeAuth(), new FakeInitializer(profile), Config, prefs);

    [Fact]
    public void Starts_on_role_choice_when_nothing_is_remembered()
    {
        var vm = Create(new InMemoryAppPreferences());

        Assert.Equal(LoginStep.ChooseRole, vm.Step);
        Assert.True(vm.IsChoosingRole);
        Assert.False(vm.IsForm);
    }

    [Fact]
    public void Choosing_a_role_opens_its_form_and_back_returns()
    {
        var vm = Create(new InMemoryAppPreferences());

        vm.ChooseAthleteCommand.Execute(null);
        Assert.Equal(LoginStep.Athlete, vm.Step);
        Assert.Equal("Είσοδος αθλητή", vm.FormTitle);
        Assert.False(vm.IsCoachForm);

        vm.BackCommand.Execute(null);
        Assert.True(vm.IsChoosingRole);

        vm.ChooseSignUpCommand.Execute(null);
        Assert.True(vm.IsSignUp);
        Assert.Equal("Δημιουργία λογαριασμού", vm.SubmitText);
    }

    [Fact]
    public async Task Successful_login_remembers_the_role_for_next_time()
    {
        var prefs = new InMemoryAppPreferences();
        var vm = Create(prefs, UserRole.Coach);
        UserRole? loggedIn = null;
        vm.LoginSucceeded += r => loggedIn = r;

        vm.ChooseCoachCommand.Execute(null);
        vm.Email = "coach@example.com";
        vm.Password = "secret1";
        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.Equal(UserRole.Coach, loggedIn);
        Assert.Equal("Coach", prefs.Get(LoginViewModel.LastRoleKey));
        Assert.Equal(LoginStep.Coach, Create(prefs).Step);
    }

    [Fact]
    public async Task Wrong_role_signs_out_and_remembers_nothing()
    {
        var prefs = new InMemoryAppPreferences();
        var auth = new FakeAuth();
        var vm = Create(prefs, UserRole.Athlete, auth);

        vm.ChooseCoachCommand.Execute(null);
        vm.Email = "athlete@example.com";
        vm.Password = "secret1";
        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.True(auth.SignedOut);
        Assert.Equal("Δεν βρέθηκε προφίλ προπονητή γι' αυτόν τον λογαριασμό.", vm.ErrorMessage);
        Assert.Null(prefs.Get(LoginViewModel.LastRoleKey));
    }

    [Fact]
    public async Task Back_clears_the_previous_forms_errors()
    {
        var vm = Create(new InMemoryAppPreferences());

        vm.ChooseCoachCommand.Execute(null);
        await vm.SubmitCommand.ExecuteAsync(null); // empty fields
        Assert.NotNull(vm.ErrorMessage);
        Assert.True(vm.HasErrors);

        vm.BackCommand.Execute(null);
        vm.ChooseAthleteCommand.Execute(null);
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.HasErrors);
    }

    [Fact]
    public async Task Sign_up_needing_email_confirmation_lands_on_the_athlete_form()
    {
        var prefs = new InMemoryAppPreferences();
        var auth = new FakeAuth { SignUpResult = new AuthResult(true, null, null, null, null) };
        var vm = Create(prefs, UserRole.Athlete, auth);

        vm.ChooseSignUpCommand.Execute(null);
        vm.Email = "new@example.com";
        vm.Password = "secret1";
        await vm.SubmitCommand.ExecuteAsync(null);

        Assert.Equal(LoginStep.Athlete, vm.Step);
        Assert.NotNull(vm.InfoMessage);
        Assert.Equal("Athlete", prefs.Get(LoginViewModel.LastRoleKey));
    }
}
