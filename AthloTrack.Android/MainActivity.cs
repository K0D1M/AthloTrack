using System;
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
        HoldLaunchScreen();

        // Android 13+: phone notifications need the user's permission.
        if (OperatingSystem.IsAndroidVersionAtLeast(33) &&
            CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) != Permission.Granted)
        {
            RequestPermissions(new[] { global::Android.Manifest.Permission.PostNotifications }, 1001);
        }

        HandleNotificationIntent(Intent); // cold start from a notification
    }

    /// <summary>
    /// Android 12+: the launch screen (navy + logo) normally fades out as soon as the app's window
    /// draws, but the app's surface is still black then. Instead it stays on top until the app's
    /// own splash, which looks the same, has drawn (App.SplashReady), and is then removed at once,
    /// so the hand-over can't be seen. At most 6 s.
    /// </summary>
    private void HoldLaunchScreen()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(31)) return;
        SplashScreen.SetOnExitAnimationListener(new RemoveWhenReady(DateTime.UtcNow + TimeSpan.FromSeconds(6)));
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android31.0")]
    private sealed class RemoveWhenReady(DateTime until)
        : Java.Lang.Object, global::Android.Window.ISplashScreenOnExitAnimationListener
    {
        public void OnSplashScreenExit(global::Android.Window.SplashScreenView view) => Check(view);

        private void Check(global::Android.Window.SplashScreenView view)
        {
            if (App.SplashReady)
            {
                // The app's first frames reach the screen a little after they are drawn.
                view.PostDelayed(view.Remove, 100);
                return;
            }
            if (DateTime.UtcNow > until)
            {
                view.Remove();
                return;
            }
            view.PostDelayed(() => Check(view), 30);
        }
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        HandleNotificationIntent(intent); // app already running
    }

    /// <summary>
    /// A tapped push carries its data keys as extras ("type" = notifications.type). The group
    /// summary carries all its notification ids (comma-separated) and "clearAll".
    /// </summary>
    private void HandleNotificationIntent(Intent? intent)
    {
        var type = intent?.GetStringExtra("type");
        if (type is null) return;
        var notificationIds = intent!.GetStringExtra("notificationId");
        if (intent.GetStringExtra("clearAll") == "1") Services.PushNotifications.ClearAll(this);
        intent.RemoveExtra("type"); // don't replay on recreate
        Dispatcher.UIThread.Post(() => NotificationNavigation.RequestFor(type, notificationIds));
    }
}
