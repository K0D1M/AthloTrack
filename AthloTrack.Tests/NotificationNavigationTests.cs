using AthloTrack.Services;

namespace AthloTrack.Tests;

// NotificationNavigation is static; keep its tests in one class so they never run in parallel.
public sealed class NotificationNavigationTests
{
    [Theory]
    [InlineData("new_workout")]
    [InlineData("workout_updated")]
    [InlineData("workout_completed")]
    public void Workout_notifications_open_the_workouts_section(string type)
    {
        NotificationNavigation.TakePending();
        var id = Guid.NewGuid();

        NotificationNavigation.RequestFor(type, id.ToString());

        var request = NotificationNavigation.TakePending();
        Assert.NotNull(request);
        Assert.Equal(NotificationNavigation.WorkoutsSection, request.Section);
        Assert.Equal(id, request.NotificationId);
    }

    [Fact]
    public void A_request_is_used_once()
    {
        NotificationNavigation.RequestFor("new_workout");

        Assert.NotNull(NotificationNavigation.TakePending());
        Assert.Null(NotificationNavigation.TakePending());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("something_else")]
    public void Other_types_are_ignored(string? type)
    {
        NotificationNavigation.TakePending();

        NotificationNavigation.RequestFor(type, Guid.NewGuid().ToString());

        Assert.Null(NotificationNavigation.TakePending());
    }

    [Fact]
    public void A_missing_or_bad_notification_id_still_opens_the_section()
    {
        NotificationNavigation.RequestFor("new_workout", "not-a-guid");

        var request = NotificationNavigation.TakePending();
        Assert.NotNull(request);
        Assert.Null(request.NotificationId);
    }
}
