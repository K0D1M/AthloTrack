using AthloTrack.Core.Auth;
using AthloTrack.Services;
using AthloTrack.ViewModels;

namespace AthloTrack.Tests;

public sealed class PinLockTests
{
    private readonly InMemoryAppPreferences _prefs = new();
    private readonly Guid _user = Guid.NewGuid();

    private PinLock Lock() => new(_prefs);

    [Fact]
    public void A_set_pin_unlocks_and_a_wrong_one_counts_down()
    {
        var pin = Lock();
        pin.Set(_user, "246810");

        Assert.True(pin.IsEnabledFor(_user));
        Assert.Equal(new PinCheck(PinCheckResult.Wrong, 4), pin.Verify("000000"));
        Assert.Equal(new PinCheck(PinCheckResult.Wrong, 3), pin.Verify("111111"));
        Assert.Equal(PinCheckResult.Ok, pin.Verify("246810").Result);
        Assert.Equal(new PinCheck(PinCheckResult.Wrong, 4), pin.Verify("000000")); // the right PIN reset the count
    }

    [Fact]
    public void The_fifth_wrong_pin_locks_out_and_removes_the_pin()
    {
        var pin = Lock();
        pin.Set(_user, "246810");

        for (var i = 0; i < 4; i++) Assert.Equal(PinCheckResult.Wrong, pin.Verify("999999").Result);
        Assert.Equal(PinCheckResult.LockedOut, pin.Verify("999999").Result);

        Assert.False(pin.IsEnabledFor(_user));
        Assert.Equal(PinCheckResult.LockedOut, pin.Verify("246810").Result);
    }

    [Fact]
    public void Wrong_attempts_survive_restarting_the_app()
    {
        Lock().Set(_user, "246810");
        Lock().Verify("000000");
        Lock().Verify("000000");

        Assert.Equal(new PinCheck(PinCheckResult.Wrong, 2), Lock().Verify("000000"));
    }

    [Fact]
    public void Another_login_never_uses_this_pin()
    {
        var pin = Lock();
        pin.Set(_user, "246810");

        Assert.False(pin.IsEnabledFor(Guid.NewGuid()));
        Assert.False(pin.IsEnabledFor(_user)); // and it was removed
    }

    [Fact]
    public void Nothing_is_stored_in_plain_text_and_the_salt_changes()
    {
        var pin = Lock();
        pin.Set(_user, "246810");
        var first = _prefs.Get("pin.hash");
        var salt = _prefs.Get("pin.salt");
        pin.Set(_user, "246810");

        Assert.DoesNotContain("246810", first);
        Assert.NotEqual(salt, _prefs.Get("pin.salt"));
        Assert.NotEqual(first, _prefs.Get("pin.hash"));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12a456")]
    public void Only_six_digits_are_a_pin(string candidate)
    {
        Assert.False(PinLock.IsValid(candidate));
        Assert.Throws<ArgumentException>(() => Lock().Set(_user, candidate));
    }

    [Fact]
    public void Only_more_than_a_minute_away_locks_on_return()
    {
        var pin = Lock();
        var t = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

        pin.NoteBackground(t);
        Assert.False(pin.ShouldLockOnReturn(t.AddSeconds(59)));

        pin.NoteBackground(t);
        Assert.True(pin.ShouldLockOnReturn(t.AddSeconds(61)));

        Assert.False(pin.ShouldLockOnReturn(t.AddHours(1))); // no new background: nothing to lock
    }

    // ---- keypad ----

    private static void Type(PinPadViewModel pad, string digits)
    {
        foreach (var d in digits) pad.Digit(d.ToString());
    }

    [Fact]
    public void Creating_needs_the_same_pin_twice()
    {
        var pad = new PinPadViewModel(Lock(), PinPadMode.Create);
        string? created = null;
        pad.Created += p => created = p;

        Type(pad, "135792");
        Assert.Equal("Ξανά, για επιβεβαίωση", pad.Subtitle);
        Assert.Equal(0, pad.Filled);
        Type(pad, "135790");
        Assert.Null(created);
        Assert.Equal("Τα PIN δεν ταιριάζουν. Ξεκίνα από την αρχή.", pad.Error);

        Type(pad, "135792");
        Type(pad, "135792");
        Assert.Equal("135792", created);
    }

    [Fact]
    public void Backspace_removes_the_last_digit_and_the_sixth_submits()
    {
        var pin = Lock();
        pin.Set(_user, "112233");
        var pad = new PinPadViewModel(pin, PinPadMode.Unlock, "Μαρία");
        var unlocked = false;
        pad.Unlocked += () => unlocked = true;

        Assert.Equal("Γεια σου, Μαρία", pad.Title);
        Assert.False(pad.HasSubtitle);
        Type(pad, "11229");
        Assert.True(pad.Dot5);
        pad.Backspace();
        Assert.Equal(4, pad.Filled);
        Type(pad, "33");

        Assert.True(unlocked);
    }

    [Fact]
    public void Wrong_pins_say_how_many_tries_are_left_then_sign_out()
    {
        var pin = Lock();
        pin.Set(_user, "112233");
        var pad = new PinPadViewModel(pin, PinPadMode.Unlock);
        bool? signOut = null;
        pad.SignOutRequested += lockedOut => signOut = lockedOut;

        Type(pad, "000000");
        Assert.Equal("Λάθος PIN· απομένουν 4 προσπάθειες.", pad.Error);
        Assert.Equal(1, pad.ShakeCount);
        for (var i = 0; i < 3; i++) Type(pad, "000000");
        Assert.Equal("Λάθος PIN· απομένει 1 προσπάθεια.", pad.Error);
        Type(pad, "000000");

        Assert.True(signOut);
    }

    [Fact]
    public void The_lock_itself_cannot_be_cancelled_but_settings_prompts_can()
    {
        Assert.False(new PinPadViewModel(Lock(), PinPadMode.Unlock).CanCancel);
        Assert.True(new PinPadViewModel(Lock(), PinPadMode.Create).CanCancel);
        Assert.False(new PinPadViewModel(Lock(), PinPadMode.Create).ShowForgot);
        Assert.True(new PinPadViewModel(Lock(), PinPadMode.ConfirmCurrent).ShowForgot);
    }
}
