using AthloTrack.Core.Auth;
using AthloTrack.Core.Models;
using AthloTrack.Core.Supabase;
using AthloTrack.ViewModels;

namespace AthloTrack.Tests;

/// <summary>Skeleton placeholders on a screen's first load only, and the login screen's "ready" moment.</summary>
public class LoadingStateTests
{
    [Fact]
    public async Task Workouts_show_the_skeleton_only_during_the_first_load()
    {
        var athleteId = Guid.NewGuid();
        var workouts = new FakeWorkouts { LoadGate = new TaskCompletionSource() };
        workouts.Items.Add(new WorkoutProgram { Id = Guid.NewGuid(), AthleteId = athleteId, Content = "run", TargetDate = new DateOnly(2026, 10, 6) });
        // The view model starts its first load itself.
        var vm = new WorkoutsViewModel(workouts, new FakeAthletes(), Sessions.Athlete(athleteId, Guid.NewGuid()));
        Assert.True(vm.IsLoading);
        Assert.True(vm.ShowSkeleton);

        workouts.LoadGate.SetResult();
        for (var i = 0; i < 100 && vm.IsLoading; i++) await Task.Delay(10);
        Assert.False(vm.ShowSkeleton);
        Assert.Single(vm.WorkoutPrograms);

        // A refresh keeps the list on screen: only the loading line shows.
        workouts.LoadGate = new TaskCompletionSource();
        var refresh = vm.LoadAsync();
        Assert.True(vm.IsLoading);
        Assert.False(vm.ShowSkeleton);
        workouts.LoadGate.SetResult();
        await refresh;
    }

    [Fact]
    public async Task The_athlete_profile_shows_the_skeleton_while_it_first_loads()
    {
        var athleteId = Guid.NewGuid();
        var athletes = new FakeAthletes();
        athletes.Items.Add(new Athlete { Id = athleteId, FullName = "Γιάννης" });
        var workouts = new FakeWorkouts { LoadGate = new TaskCompletionSource() };
        var vm = new AthleteProfileViewModel(athleteId, athletes, new FakeMeasurements(), workouts,
            Sessions.Coach(Guid.NewGuid()), new FakeAvatars());

        var load = vm.LoadAsync();
        Assert.True(vm.ShowSkeleton);
        Assert.False(vm.HasNoWorkouts); // no "nothing yet" message under the placeholders

        workouts.LoadGate.SetResult();
        await load;
        Assert.False(vm.ShowSkeleton);
        Assert.True(vm.HasNoWorkouts);
    }

    private sealed class NoSession : IAuthService
    {
        public Task<AuthResult> SignInAsync(string email, string password) => Task.FromResult(AuthResult.Fail("x"));
        public Task<AuthResult> RestoreSessionAsync() => Task.FromResult(AuthResult.Fail("none"));
        public Task<AuthResult> SignUpAsync(string email, string password) => Task.FromResult(AuthResult.Fail("x"));
        public Task<AuthResult> UpdatePasswordAsync(string newPassword) => Task.FromResult(AuthResult.Fail("x"));
        public Task SignOutAsync() => Task.CompletedTask;
    }

    private sealed class NoProfile : ISessionInitializer
    {
        public Task<bool> InitializeAsync(UserRole role, Guid authUserId) => Task.FromResult(false);
    }

    [Fact]
    public async Task The_login_screen_is_ready_once_the_silent_sign_in_is_over()
    {
        var vm = new LoginViewModel(new NoSession(), new NoProfile(),
            new SupabaseConfig { Url = "https://x.supabase.co", AnonKey = "key" }, new InMemoryAppPreferences());
        Assert.False(vm.IsReady);

        await vm.TryRestoreSessionAsync();

        Assert.True(vm.IsReady);
        Assert.False(vm.IsBusy);
    }
}
