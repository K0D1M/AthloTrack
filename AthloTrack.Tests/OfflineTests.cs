using System.Net.Http;
using System.Reflection;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;
using AthloTrack.Core.Models;
using AthloTrack.Core.Offline;
using AthloTrack.Core.Supabase;
using AthloTrack.ViewModels;

namespace AthloTrack.Tests;

/// <summary>Προπονήσεις and Ημερολόγιο without internet (Android): copies, queued answers, offline start.</summary>
public sealed class OfflineTests
{
    /// <summary>Forwards to the real fake, or throws like a phone without signal.</summary>
    public class Flaky<T> : DispatchProxy where T : class
    {
        public T Inner = null!;
        public bool Offline;

        public static (T Proxy, Flaky<T> Switch) Wrap(T inner)
        {
            var proxy = Create<T, Flaky<T>>();
            var flaky = (Flaky<T>)(object)proxy;
            flaky.Inner = inner;
            return (proxy, flaky);
        }

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (Offline)
            {
                var error = new HttpRequestException("No such host is known.");
                var type = method!.ReturnType;
                if (type == typeof(Task)) return Task.FromException(error);
                var inner = type.GetGenericArguments()[0];
                return typeof(Task).GetMethod(nameof(Task.FromException), 1, [typeof(Exception)])!
                    .MakeGenericMethod(inner).Invoke(null, [error]);
            }
            try
            {
                return method!.Invoke(Inner, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        }
    }

    private readonly Guid _user = Guid.NewGuid();
    private readonly SessionState _session = new();
    private readonly MemoryOfflineStore _store = new();
    private readonly OfflineState _state = new();
    private readonly FakeWorkouts _fake = new();
    private readonly OfflineCache _cache;
    private readonly Flaky<IWorkoutRepository> _net;
    private readonly CachedWorkoutRepository _workouts;

    public OfflineTests()
    {
        _session.AuthUserId = _user;
        _cache = new OfflineCache(_store, _session);
        var (proxy, net) = Flaky<IWorkoutRepository>.Wrap(_fake);
        _net = net;
        _workouts = new CachedWorkoutRepository(proxy, _cache, _state);
    }

    private WorkoutProgram Add(DateOnly date)
    {
        var w = new WorkoutProgram { Id = Guid.NewGuid(), AthleteId = Guid.NewGuid(), Content = "τρέξιμο", TargetDate = date };
        _fake.Items.Add(w);
        return w;
    }

    [Fact]
    public async Task Online_the_list_is_saved_and_offline_it_comes_from_the_copy()
    {
        var w = Add(new DateOnly(2026, 11, 24));
        Assert.Single(await _workouts.GetAllAsync());
        Assert.False(_state.IsOffline);

        _net.Offline = true;
        var offline = await _workouts.GetAllAsync();

        Assert.Equal(w.Id, Assert.Single(offline).Id);
        Assert.Equal("τρέξιμο", offline[0].Content);
        Assert.True(_state.IsOffline);
        Assert.NotNull(_state.SavedAt);
    }

    [Fact]
    public async Task Without_a_copy_offline_says_so_in_Greek()
    {
        _net.Offline = true;

        var ex = await Assert.ThrowsAsync<OfflineException>(_workouts.GetAllAsync);

        Assert.Equal(OfflineException.NoInternet, ex.Message);
    }

    [Fact]
    public async Task An_answer_offline_is_shown_at_once_and_sent_when_back_online()
    {
        var w = Add(new DateOnly(2026, 11, 24));
        await _workouts.GetAllAsync();
        _net.Offline = true;

        await _workouts.AcceptAsync(w.Id);
        await _workouts.MarkCompletedAsync(w.Id);

        Assert.Empty(_fake.Accepted); // not sent
        Assert.Equal(2, _state.PendingAnswers);
        var shown = Assert.Single(await _workouts.GetAllAsync());
        Assert.True(shown.IsAccepted);
        Assert.True(shown.IsCompleted);
        Assert.True(shown.IsPendingSync);

        var reconnected = false;
        _state.Reconnected += () => reconnected = true;
        _net.Offline = false;
        await _workouts.GetAllAsync();

        Assert.Equal([w.Id], _fake.Accepted);
        Assert.Equal([w.Id], _fake.Completed); // in order: accepted, then completed
        Assert.Equal(0, _state.PendingAnswers);
        Assert.Empty(_cache.LoadPending());
        Assert.True(reconnected);
        Assert.False(_state.IsOffline);
    }

    [Fact]
    public async Task A_queued_answer_the_server_refuses_is_dropped_and_the_user_told()
    {
        var w = Add(new DateOnly(2026, 11, 24));
        await _workouts.GetAllAsync();
        _net.Offline = true;
        await _workouts.CoachAnswerAsync(w.Id, completed: true);

        // Meanwhile the workout was deleted on the server: the fake refuses with an error from the server.
        var refusing = new CachedWorkoutRepository(new RefusingWorkouts(), _cache, _state);
        string? notice = null;
        _state.Notice += m => notice = m;
        await refusing.FlushAsync();

        Assert.Empty(_cache.LoadPending());
        Assert.Contains("24/11", notice);
        Assert.Contains("ακυρώθηκε", notice);
    }

    private sealed class RefusingWorkouts : IWorkoutRepository
    {
        private static Task Refuse() => Task.FromException(new CoachAnswerException("ακυρώθηκε"));
        public Task CoachAnswerAsync(Guid id, bool completed) => Refuse();
        public Task AcceptAsync(Guid id) => Refuse();
        public Task DeclineAsync(Guid id, string? reason) => Refuse();
        public Task MarkCompletedAsync(Guid id) => Refuse();
        public Task MarkNotCompletedAsync(Guid id) => Refuse();
        public Task<Guid> AddPastAsync(Guid a, string c, DateOnly d, TimeOnly? t, bool done, bool? p) => throw new NotSupportedException();
        public Task<IReadOnlyList<WorkoutProgram>> GetAllAsync() => throw new NotSupportedException();
        public Task<IReadOnlyList<WorkoutProgram>> GetForAthleteAsync(Guid athleteId) => throw new NotSupportedException();
        public Task<WorkoutProgram> AddAsync(WorkoutProgram program) => throw new NotSupportedException();
        public Task CancelAsync(Guid id) => throw new NotSupportedException();
        public Task RestoreAsync(Guid id) => throw new NotSupportedException();
        public Task CoachUndoAnswerAsync(Guid id) => throw new NotSupportedException();
        public Task RemindAsync(Guid id) => throw new NotSupportedException();
        public Task<WorkoutProgram?> GetByIdAsync(Guid id) => throw new NotSupportedException();
        public Task UpdateAsync(Guid id, string content, DateOnly targetDate, bool? coachPresent, TimeOnly? startTime = null) => throw new NotSupportedException();
        public Task MarkReadAsync(Guid id) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Other_changes_offline_need_the_internet()
    {
        var w = Add(new DateOnly(2026, 11, 24));
        await _workouts.GetAllAsync();
        _net.Offline = true;

        var ex = await Assert.ThrowsAsync<OfflineException>(() => _workouts.RemindAsync(w.Id));
        Assert.Equal(OfflineException.NeedsInternet, ex.Message);
        await _workouts.MarkReadAsync(w.Id); // a read receipt waits silently
    }

    [Fact]
    public async Task Where_nothing_is_kept_an_offline_answer_is_not_queued()
    {
        var cache = new OfflineCache(new NullOfflineStore(), _session);
        var (proxy, net) = Flaky<IWorkoutRepository>.Wrap(_fake);
        var workouts = new CachedWorkoutRepository(proxy, cache, _state);
        var w = Add(new DateOnly(2026, 11, 24));
        await workouts.GetAllAsync();
        net.Offline = true;

        var ex = await Assert.ThrowsAsync<OfflineException>(() => workouts.AcceptAsync(w.Id));

        Assert.Equal(OfflineException.NeedsInternet, ex.Message);
        Assert.Equal(0, _state.PendingAnswers);
    }

    [Fact]
    public async Task Athletes_and_measurements_for_the_calendar_come_from_the_copy()
    {
        var athletes = new FakeAthletes();
        athletes.Items.Add(new Athlete { Id = Guid.NewGuid(), FullName = "Μαρία" });
        var (aProxy, aNet) = Flaky<IAthleteRepository>.Wrap(athletes);
        var cachedAthletes = new CachedAthleteRepository(aProxy, _cache, _state);
        var measures = new FakeMeasurements();
        measures.Items.Add(new Measurement { Id = Guid.NewGuid(), Title = "Τεστ Cooper", Extra = { [Guid.NewGuid()] = 2450 } });
        var (mProxy, mNet) = Flaky<IMeasurementRepository>.Wrap(measures);
        var cachedMeasures = new CachedMeasurementRepository(mProxy, _cache, _state);
        await cachedAthletes.GetAllAsync();
        await cachedMeasures.GetAllAsync();

        aNet.Offline = mNet.Offline = true;

        Assert.Equal("Μαρία", Assert.Single(await cachedAthletes.GetAllAsync()).FullName);
        var m = Assert.Single(await cachedMeasures.GetAllAsync());
        Assert.Equal("Τεστ Cooper", m.Title);
        Assert.Equal(2450, m.Extra.Values.Single());
    }

    [Fact]
    public void Sign_out_removes_every_copy()
    {
        _cache.SaveWorkouts([new WorkoutProgram { Id = Guid.NewGuid() }]);
        _cache.SaveSession(_user, new SessionSnapshot(UserRole.Athlete, Guid.NewGuid(), "Μαρία", null, null));
        Assert.NotEmpty(_store.Items);

        _cache.ClearAll();

        Assert.Empty(_store.Items);
        Assert.Null(_cache.LoadWorkouts());
    }

    [Fact]
    public void Network_errors_are_told_apart_from_server_answers()
    {
        Assert.True(OfflineState.IsNetworkError(new HttpRequestException("No such host is known.")));
        Assert.True(OfflineState.IsNetworkError(new TaskCanceledException()));
        Assert.True(OfflineState.IsNetworkError(new Exception("wrap", new System.Net.Sockets.SocketException())));
        Assert.False(OfflineState.IsNetworkError(new HttpRequestException("Forbidden", null, System.Net.HttpStatusCode.Forbidden)));
        Assert.False(OfflineState.IsNetworkError(new InvalidOperationException("RLS")));
    }

    // ---- opening the app without internet ----

    [Fact]
    public void A_snapshot_from_the_last_sign_in_restores_the_session()
    {
        var athleteId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        _cache.SaveSession(_user, new SessionSnapshot(UserRole.Athlete, athleteId, "Μαρία Κ.", "p.jpg", coachId));
        var session = new SessionState();
        var init = new SessionInitializer(session, null!, null!, null!, new OfflineCache(_store, session), _state);

        Assert.Equal(UserRole.Athlete, init.RestoreOffline(_user));
        Assert.Equal(athleteId, session.ProfileId);
        Assert.Equal(coachId, session.CoachId);
        Assert.Equal("Μαρία Κ.", session.DisplayName);
        Assert.True(_state.IsOffline);
        Assert.Null(init.RestoreOffline(Guid.NewGuid())); // another login: the form
    }

    private sealed class OfflineAuth(Guid user) : IAuthService
    {
        public bool SignedOut;
        public Task<AuthResult> SignInAsync(string email, string password) => Task.FromResult(AuthResult.Fail("x"));
        public Task<AuthResult> RestoreSessionAsync() => Task.FromResult(AuthResult.Offline(user));
        public Task<AuthResult> SignUpAsync(string email, string password) => Task.FromResult(AuthResult.Fail("x"));
        public Task<AuthResult> UpdatePasswordAsync(string newPassword) => Task.FromResult(AuthResult.Fail("x"));
        public Task SignOutAsync() { SignedOut = true; return Task.CompletedTask; }
    }

    private sealed class SnapshotInitializer(UserRole? saved) : ISessionInitializer
    {
        public Task<bool> InitializeAsync(UserRole role, Guid authUserId) => Task.FromResult(false);
        public UserRole? RestoreOffline(Guid authUserId) => saved;
    }

    private static LoginViewModel Login(IAuthService auth, ISessionInitializer init) =>
        new(auth, init, new SupabaseConfig { Url = "https://x.supabase.co", AnonKey = "k" }, new InMemoryAppPreferences());

    [Fact]
    public async Task Opening_the_app_offline_enters_from_the_snapshot_and_keeps_the_sign_in()
    {
        var auth = new OfflineAuth(_user);
        var vm = Login(auth, new SnapshotInitializer(UserRole.Athlete));
        UserRole? entered = null;
        vm.LoginSucceeded += r => entered = r;

        await vm.TryRestoreSessionAsync();

        Assert.Equal(UserRole.Athlete, entered);
        Assert.True(vm.LastLoginWasRestored); // the PIN lock still applies
        Assert.False(auth.SignedOut);
    }

    [Fact]
    public async Task Offline_without_a_snapshot_shows_the_login_form()
    {
        var auth = new OfflineAuth(_user);
        var vm = Login(auth, new SnapshotInitializer(null));
        var entered = false;
        vm.LoginSucceeded += _ => entered = true;

        await vm.TryRestoreSessionAsync();

        Assert.False(entered);
        Assert.True(vm.IsReady);
        Assert.False(auth.SignedOut); // the saved sign-in stays for when the internet is back
    }
}
