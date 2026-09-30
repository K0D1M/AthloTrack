using Android.App;
using Android.Content;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using Avalonia.Threading;
using AthloTrack.Services;

namespace AthloTrack.Android;

// SingleTop: tapping a notification (FCM adds CLEAR_TOP) reaches the running activity through
// OnNewIntent instead of recreating it; the app has one root view that can't be re-hosted.
[Activity(
    Label = "AthloTrack",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(global::Android.OS.Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Android 13+: phone notifications need the user's permission.
        if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.Tiramisu &&
            CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) != Permission.Granted)
        {
            RequestPermissions(new[] { global::Android.Manifest.Permission.PostNotifications }, 1001);
        }

        HandleNotificationIntent(Intent); // cold start from a notification
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        HandleNotificationIntent(intent); // app already running
    }

    /// <summary>A tapped push carries its data keys as extras ("type" = notifications.type).</summary>
    private static void HandleNotificationIntent(Intent? intent)
    {
        var type = intent?.GetStringExtra("type");
        if (type is null) return;
        intent!.RemoveExtra("type"); // don't replay on recreate
        Dispatcher.UIThread.Post(() => NotificationNavigation.RequestFor(type));
    }
}
