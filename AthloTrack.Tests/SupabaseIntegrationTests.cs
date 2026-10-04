using AthloTrack.Core.Data;
using AthloTrack.Core.Supabase;

namespace AthloTrack.Tests;

/// <summary>
/// Runs the real queries against the Supabase project, signed in as the test accounts, so that
/// queries PostgREST rejects (like the athlete-notification filter, PGRST100) fail here first.
/// Skipped unless these environment variables are set (test accounts only, never real ones):
///   ATHLOTRACK_TEST_ATHLETE_EMAIL / ATHLOTRACK_TEST_ATHLETE_PASSWORD
///   ATHLOTRACK_TEST_COACH_EMAIL   / ATHLOTRACK_TEST_COACH_PASSWORD
/// </summary>
public sealed class SupabaseIntegrationTests
{
    private static readonly SupabaseConfig Config = LoadConfig();

    private static SupabaseConfig LoadConfig()
    {
        // The same file the app embeds.
        var path = Path.Combine(AppContext.BaseDirectory, "supabase.config.json");
        var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)).RootElement;
        return new SupabaseConfig { Url = json.GetProperty("Url").GetString()!, AnonKey = json.GetProperty("AnonKey").GetString()! };
    }

    private static async Task<(SupabaseClientFactory Factory, Guid AuthUserId)> SignIn(string role)
    {
        var email = Environment.GetEnvironmentVariable($"ATHLOTRACK_TEST_{role}_EMAIL");
        var password = Environment.GetEnvironmentVariable($"ATHLOTRACK_TEST_{role}_PASSWORD");
        Assert.SkipWhen(string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password),
            $"ATHLOTRACK_TEST_{role}_EMAIL/_PASSWORD not set");

        var factory = new SupabaseClientFactory(Config);
        var client = await factory.GetClientAsync();
        var session = await client.Auth.SignInWithPassword(email!, password!);
        return (factory, Guid.Parse(session!.User!.Id!));
    }

    [Fact]
    public async Task The_athlete_home_queries_succeed()
    {
        var (factory, authUserId) = await SignIn("ATHLETE");
        var athlete = await new AthleteRepository(factory).GetByAuthUserIdAsync(authUserId);
        Assert.NotNull(athlete);

        // Πρόσφατα: the athlete's unread notifications, then their coach card.
        await new NotificationRepository(factory).GetUnreadForAthleteAsync(athlete.Id);
        Assert.NotNull(await new CoachRepository(factory).GetByIdAsync(athlete.CoachId));
        await new WorkoutRepository(factory).GetForAthleteAsync(athlete.Id);
    }

    [Fact]
    public async Task The_coach_home_queries_succeed()
    {
        var (factory, authUserId) = await SignIn("COACH");
        Assert.NotNull(await new CoachRepository(factory).GetByAuthUserIdAsync(authUserId));

        await new NotificationRepository(factory).GetUnreadForCoachAsync();
        await new WorkoutRepository(factory).GetAllAsync();
        await new AthleteRepository(factory).GetAllAsync();
    }

    [Fact]
    public async Task Editing_a_workout_keeps_its_date()
    {
        // Saving with unchanged text, date and presence: nothing changes (no notification, receipt kept),
        // but a time-zone slip in the update would move the date to the previous day.
        var (factory, _) = await SignIn("COACH");
        var repository = new WorkoutRepository(factory);
        var workout = (await repository.GetAllAsync()).FirstOrDefault();
        Assert.SkipWhen(workout is null, "the test coach has no workouts");

        await repository.UpdateAsync(workout!.Id, workout.Content, workout.TargetDate, workout.CoachPresent);

        var reloaded = (await repository.GetAllAsync()).Single(w => w.Id == workout.Id);
        Assert.Equal(workout.TargetDate, reloaded.TargetDate);
        Assert.Equal(workout.CoachPresent, reloaded.CoachPresent);
        Assert.Equal(workout.ReadAt, reloaded.ReadAt);
    }
}
