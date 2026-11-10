using AthloTrack.Core.Models;
using AthloTrack.ViewModels;

namespace AthloTrack.Tests;

public sealed class NotificationItemTests
{
    private static readonly DateTimeOffset Now = new(new DateTime(2026, 11, 10, 20, 0, 0, DateTimeKind.Local));

    [Theory]
    [InlineData("Ο/Η Μαρία Κ. ολοκλήρωσε το ασκησιολόγιο της 10/11", "Μαρία Κ.", "ολοκλήρωσε το ασκησιολόγιο της 10/11")]
    [InlineData("Ο Γιώργος Π. ακύρωσε το ασκησιολόγιο της 12/11", "Γιώργος Π.", "ακύρωσε το ασκησιολόγιο της 12/11")]
    [InlineData("Μαρία Κ. υπέβαλε το έντυπο «Ιστορικό»", "Μαρία Κ.", "υπέβαλε το έντυπο «Ιστορικό»")]
    public void The_name_is_split_from_the_rest(string message, string name, string rest)
    {
        Assert.Equal((name, rest), NotificationItem.Split(message, name));
    }

    [Theory]
    [InlineData("Νέο ασκησιολόγιο για τις 10/11", "Μαρία Κ.")]   // doesn't name them
    [InlineData("Θυμήσου: η Μαρία Κ. περιμένει", "Μαρία Κ.")]    // named, but not first
    [InlineData("Ο/Η Μαρία Κ. ολοκλήρωσε", null)]                 // no person known
    public void A_message_that_does_not_start_with_the_name_stays_whole(string message, string? name)
    {
        Assert.Equal((string.Empty, message), NotificationItem.Split(message, name));
    }

    [Theory]
    [InlineData("workout_completed", "done")]
    [InlineData("workout_not_completed", "notdone")]
    [InlineData("workout_accepted", "accepted")]
    [InlineData("workout_declined", "declined")]
    [InlineData("new_form", "form")]
    [InlineData("form_submitted", "form")]
    [InlineData("workout_reminder", "reminder")]
    [InlineData("workout_cancelled", "cancelled")]
    [InlineData("workout_restored", "restored")]
    [InlineData("new_workout", "workout")]
    [InlineData("workout_updated", "workout")]
    [InlineData("something_new", "workout")]
    public void Each_type_has_its_badge(string type, string kind)
    {
        Assert.Equal(kind, NotificationItem.KindOf(type));
    }

    [Fact]
    public void An_item_shows_the_bold_name_the_rest_and_the_day()
    {
        var n = new AppNotification
        {
            Type = "workout_completed",
            Message = "Ο/Η Μαρία Κ. ολοκλήρωσε το ασκησιολόγιο της 10/11",
            CreatedAt = Now.AddHours(-1),
        };

        var item = new NotificationItem(n, "Μαρία Κ.", Now);

        Assert.Equal("Μαρία Κ. ", item.NameLead);
        Assert.Equal("ολοκλήρωσε το ασκησιολόγιο της 10/11", item.Rest);
        Assert.Equal("Μ", item.Initial);
        Assert.Equal("σήμερα 19:00", item.WhenText);
        Assert.True(item.IsDone);
        Assert.False(item.IsForm);
    }

    [Fact]
    public void Without_a_name_the_whole_message_shows_with_no_bold_part()
    {
        var n = new AppNotification { Type = "new_workout", Message = "Νέο ασκησιολόγιο", CreatedAt = Now };

        var item = new NotificationItem(n, null, Now);

        Assert.False(item.HasName);
        Assert.Equal(string.Empty, item.NameLead);
        Assert.Equal("Νέο ασκησιολόγιο", item.Rest);
        Assert.True(item.IsWorkout);
    }

    [Fact]
    public void The_day_reads_today_yesterday_or_the_date()
    {
        Assert.Equal("σήμερα 08:05", TimeText.Day(Now.Date.AddHours(8).AddMinutes(5) is var t ? new DateTimeOffset(t) : default, Now));
        Assert.Equal("χθες 21:10", TimeText.Day(new DateTimeOffset(new DateTime(2026, 11, 9, 21, 10, 0, DateTimeKind.Local)), Now));
        Assert.Equal("07/10 18:32", TimeText.Day(new DateTimeOffset(new DateTime(2026, 10, 7, 18, 32, 0, DateTimeKind.Local)), Now));
    }
}
