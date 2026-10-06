using AthloTrack.Core.Data;
using AthloTrack.Core.Models.Admin;
using AthloTrack.ViewModels;

namespace AthloTrack.Tests;

public class AdminTests
{
    /// <summary>In-memory stand-in for the admin RPCs and Edge Function.</summary>
    private sealed class FakeAdmin : IAdminRepository
    {
        public List<AdminCoach> Coaches { get; } = new();
        public List<AdminAthlete> Athletes { get; } = new();
        public List<string> Calls { get; } = new();
        public string? FailWith { get; set; }

        public Task<AdminProfile?> GetByAuthUserIdAsync(Guid authUserId) => Task.FromResult<AdminProfile?>(null);
        public Task<AdminOverview> GetOverviewAsync() => Task.FromResult(new AdminOverview());
        public Task<IReadOnlyList<ActivityItem>> GetActivityAsync(int limit = 50) =>
            Task.FromResult<IReadOnlyList<ActivityItem>>(new List<ActivityItem>());
        public Task<HealthReport> GetHealthAsync() => Task.FromResult(new HealthReport());
        public Task<IReadOnlyList<AdminCoach>> GetCoachesAsync() => Task.FromResult<IReadOnlyList<AdminCoach>>(Coaches.ToList());
        public Task<IReadOnlyList<AdminAthlete>> GetAthletesAsync() => Task.FromResult<IReadOnlyList<AdminAthlete>>(Athletes.ToList());
        public Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync() =>
            Task.FromResult<IReadOnlyList<DeviceInfo>>(new List<DeviceInfo>());
        public Task<IReadOnlyList<AuditEntry>> GetAuditLogAsync(int limit = 100) =>
            Task.FromResult<IReadOnlyList<AuditEntry>>(new List<AuditEntry>());

        public Task ForcePasswordChangeAsync(Guid coachId) => Record($"force {coachId}");

        public Task MoveAthleteAsync(Guid athleteId, Guid coachId)
        {
            var a = Athletes.Single(x => x.Id == athleteId);
            a.CoachId = coachId;
            return Record($"move {athleteId} {coachId}");
        }

        public Task DeleteAthleteAsync(Guid athleteId)
        {
            Athletes.RemoveAll(x => x.Id == athleteId);
            return Record($"delete-athlete {athleteId}");
        }

        public async Task<AdminActionResult> CreateCoachAsync(string email, string fullName)
        {
            await Record($"create {email}");
            Coaches.Add(new AdminCoach { Id = Guid.NewGuid(), AuthUserId = Guid.NewGuid(), FullName = fullName, Email = email, MustSetPassword = true });
            return new AdminActionResult { TempPassword = "Temp-Pass-123" };
        }

        public async Task<AdminActionResult> ResetPasswordAsync(Guid authUserId)
        {
            await Record($"reset {authUserId}");
            return new AdminActionResult { TempPassword = "New-Temp-456" };
        }

        public Task DeleteLoginAsync(Guid authUserId)
        {
            Coaches.RemoveAll(c => c.AuthUserId == authUserId);
            return Record($"delete-login {authUserId}");
        }

        public async Task<AdminActionResult> SendTestPushAsync(Guid authUserId)
        {
            await Record($"test {authUserId}");
            return new AdminActionResult { Sent = 1 };
        }

        public async Task<AdminActionResult> BroadcastAsync(BroadcastAudience audience, string title, string body)
        {
            await Record($"broadcast {audience} {title}|{body}");
            return new AdminActionResult { Sent = 3, Users = 2 };
        }

        private Task Record(string call)
        {
            if (FailWith is not null) throw new AdminActionException(FailWith);
            Calls.Add(call);
            return Task.CompletedTask;
        }
    }

    private static AdminCoach Coach(string name, string email, int athletes = 0) => new()
    {
        Id = Guid.NewGuid(), AuthUserId = Guid.NewGuid(), FullName = name, Email = email, Athletes = athletes,
    };

    // ---- Overview ----

    [Fact]
    public void Kpis_show_counts_and_rates()
    {
        var kpis = AdminOverviewViewModel.BuildKpis(new AdminOverview
        {
            Coaches = 2, Athletes = 10, LinkedAthletes = 7, Due30Days = 8, Completed30Days = 6, Read30Days = 0,
        }).ToList();

        Assert.Equal("2", kpis.Single(k => k.Title == "Προπονητές").Value);
        Assert.Equal("7 με λογαριασμό", kpis.Single(k => k.Title == "Αθλητές").Detail);
        Assert.Equal("75%", kpis.Single(k => k.Title == "Ολοκλήρωση").Value);
        Assert.Equal("0%", kpis.Single(k => k.Title == "Διαβάστηκαν").Value);
    }

    [Fact]
    public void Rates_without_due_workouts_show_a_dash()
    {
        var kpis = AdminOverviewViewModel.BuildKpis(new AdminOverview()).ToList();

        Assert.Equal("—", kpis.Single(k => k.Title == "Ολοκλήρωση").Value);
    }

    [Fact]
    public void Health_checks_count_their_rows()
    {
        var checks = AdminOverviewViewModel.BuildChecks(new HealthReport
        {
            AthletesNoEmail = { new HealthItem { Title = "A" }, new HealthItem { Title = "B" } },
        }).ToList();

        Assert.Equal(6, checks.Count);
        var noEmail = checks.Single(c => c.Title == "Αθλητές χωρίς email");
        Assert.Equal(2, noEmail.Count);
        Assert.True(noEmail.HasIssues);
        Assert.All(checks.Where(c => c != noEmail), c => Assert.False(c.HasIssues));
    }

    [Fact]
    public void Ago_reads_naturally()
    {
        var now = new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal("ποτέ", AdminText.Ago(null, now));
        Assert.Equal("μόλις τώρα", AdminText.Ago(now.AddSeconds(-20), now));
        Assert.Equal("πριν 1 ώρα", AdminText.Ago(now.AddMinutes(-61), now));
        Assert.Equal("πριν 3 ημέρες", AdminText.Ago(now.AddDays(-3), now));
    }

    // ---- Confirmation ----

    [Fact]
    public async Task Typed_confirmation_is_needed_before_the_action_runs()
    {
        var prompt = new ConfirmPrompt();
        var ran = false;
        prompt.Ask("Sure?", "Διαγραφή", () => { ran = true; return Task.CompletedTask; }, danger: true, requiredText: "coach@x.gr");

        Assert.True(prompt.IsOpen);
        Assert.False(prompt.ConfirmCommand.CanExecute(null));

        prompt.Input = "COACH@x.gr ";
        Assert.True(prompt.ConfirmCommand.CanExecute(null));
        await prompt.ConfirmCommand.ExecuteAsync(null);

        Assert.True(ran);
        Assert.False(prompt.IsOpen);
    }

    // ---- Coaches ----

    [Fact]
    public async Task New_coach_needs_a_name_and_a_valid_unique_email()
    {
        var admin = new FakeAdmin();
        admin.Coaches.Add(Coach("Existing", "taken@x.gr"));
        var vm = new AdminCoachesViewModel(admin);
        await vm.LoadAsync();

        vm.StartCreateCommand.Execute(null);
        vm.NewName = "Νέος";
        vm.NewEmail = "not-an-email";
        await vm.CreateAsync();
        Assert.Equal("Γράψε ένα έγκυρο email.", vm.FormError);

        vm.NewEmail = "TAKEN@x.gr";
        await vm.CreateAsync();
        Assert.Equal("Υπάρχει ήδη προπονητής με αυτό το email.", vm.FormError);

        Assert.Empty(admin.Calls);
    }

    [Fact]
    public async Task Creating_a_coach_shows_the_temporary_password_once()
    {
        var admin = new FakeAdmin();
        var vm = new AdminCoachesViewModel(admin);
        await vm.LoadAsync();

        vm.StartCreateCommand.Execute(null);
        vm.NewName = "Μαρία Π.";
        vm.NewEmail = "maria@x.gr";
        await vm.CreateAsync();

        Assert.Null(vm.FormError);
        Assert.False(vm.IsCreating);
        Assert.Equal("Temp-Pass-123", vm.Issued?.Password);
        Assert.Single(vm.Coaches);

        vm.DismissIssuedCommand.Execute(null);
        Assert.False(vm.HasIssuedPassword);
    }

    [Fact]
    public async Task Deleting_a_coach_waits_for_the_typed_email()
    {
        var admin = new FakeAdmin();
        var coach = Coach("Γιώργος", "giorgos@x.gr", athletes: 3);
        admin.Coaches.Add(coach);
        var vm = new AdminCoachesViewModel(admin);
        await vm.LoadAsync();

        vm.DeleteCommand.Execute(vm.Coaches.Single());
        Assert.True(vm.Confirm.IsOpen);
        Assert.Contains("3 αθλητές", vm.Confirm.Message);
        Assert.False(vm.Confirm.ConfirmCommand.CanExecute(null));
        Assert.Empty(admin.Calls);

        vm.Confirm.Input = "giorgos@x.gr";
        await vm.Confirm.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal($"delete-login {coach.AuthUserId}", admin.Calls.Single());
        Assert.Empty(vm.Coaches);
    }

    [Fact]
    public async Task A_refused_action_shows_the_servers_message()
    {
        var admin = new FakeAdmin();
        admin.Coaches.Add(Coach("Γιώργος", "giorgos@x.gr"));
        var vm = new AdminCoachesViewModel(admin);
        await vm.LoadAsync();
        admin.FailWith = "Δεν έχεις δικαιώματα διαχειριστή.";

        vm.ResetPasswordCommand.Execute(vm.Coaches.Single());
        await vm.Confirm.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal("Δεν έχεις δικαιώματα διαχειριστή.", vm.ErrorMessage);
        Assert.Null(vm.Issued);
        Assert.False(vm.IsWorking);
    }

    // ---- Athletes ----

    [Fact]
    public async Task Athletes_filter_by_coach_link_and_search_and_move()
    {
        var admin = new FakeAdmin();
        var c1 = Coach("Coach A", "a@x.gr");
        var c2 = Coach("Coach B", "b@x.gr");
        admin.Coaches.AddRange([c1, c2]);
        var linked = new AdminAthlete { Id = Guid.NewGuid(), FullName = "Ελένη", CoachId = c1.Id, CoachName = c1.FullName, AuthUserId = Guid.NewGuid() };
        var unlinked = new AdminAthlete { Id = Guid.NewGuid(), FullName = "Νίκος", CoachId = c2.Id, CoachName = c2.FullName };
        admin.Athletes.AddRange([linked, unlinked]);

        var vm = new AdminAthletesViewModel(admin);
        await vm.LoadAsync();
        Assert.Equal(2, vm.Athletes.Count);
        Assert.Equal(3, vm.CoachFilterChoices.Count); // "every coach" + 2

        vm.ShowUnlinked = true;
        Assert.Equal("Νίκος", vm.Athletes.Single().FullName);

        vm.ShowAll = true;
        vm.CoachFilter = vm.CoachFilterChoices.Single(c => c.Id == c1.Id);
        Assert.Equal("Ελένη", vm.Athletes.Single().FullName);

        vm.CoachFilter = vm.CoachFilterChoices[0];
        vm.Search = "νικ";
        Assert.Equal("Νίκος", vm.Athletes.Single().FullName);

        // Move Νίκος to Coach A: the current coach isn't offered.
        vm.MoveCommand.Execute(vm.Athletes.Single());
        Assert.True(vm.IsMoving);
        Assert.Equal(c1.Id, vm.MoveTargets.Single().Id);
        Assert.False(vm.ConfirmMoveCommand.CanExecute(null));
        vm.MoveTarget = vm.MoveTargets.Single();
        await vm.ConfirmMoveCommand.ExecuteAsync(null);

        Assert.Equal($"move {unlinked.Id} {c1.Id}", admin.Calls.Single());
        Assert.False(vm.IsMoving);
    }

    // ---- Push ----

    [Fact]
    public async Task Announcement_needs_text_then_confirmation()
    {
        var admin = new FakeAdmin();
        var vm = new AdminPushViewModel(admin);

        vm.SendCommand.Execute(null);
        Assert.Equal("Γράψε το κείμενο της ανακοίνωσης.", vm.FormError);
        Assert.False(vm.Confirm.IsOpen);

        vm.ToCoaches = true;
        vm.Body = "Νέα έκδοση!";
        vm.SendCommand.Execute(null);
        Assert.True(vm.Confirm.IsOpen);
        await vm.Confirm.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal("broadcast Coaches |Νέα έκδοση!", admin.Calls.Single());
        Assert.Contains("3 συσκευές", vm.StatusMessage);
        Assert.Equal(string.Empty, vm.Body);
    }
}
