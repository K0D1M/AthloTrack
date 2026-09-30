using System;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Gms.Tasks;
using Android.OS;
using AthloTrack.Core.Push;
using Firebase.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace AthloTrack.Android.Services;

/// <summary>Notification channel + helpers shared by the push pieces.</summary>
public static class PushNotifications
{
    public const string ChannelId = "athlotrack"; // matches android.notification.channel_id in the push function

    public static void EnsureChannel(Context context)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;
        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        manager?.CreateNotificationChannel(new NotificationChannel(ChannelId, "AthloTrack", NotificationImportance.High)
        {
            Description = "Νέα ασκησιολόγια και ολοκληρωμένες προπονήσεις",
        });
    }

    /// <summary>Shows a notification while the app is open (in the background FCM shows it itself).</summary>
    public static void Show(Context context, string title, string body)
    {
        EnsureChannel(context);
        var launch = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!);
        var pending = launch is null ? null : PendingIntent.GetActivity(context, 0, launch,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

#pragma warning disable CA1422 // pre-26 fallback constructor
        var builder = Build.VERSION.SdkInt >= BuildVersionCodes.O
            ? new Notification.Builder(context, ChannelId)
            : new Notification.Builder(context);
#pragma warning restore CA1422
        builder.SetContentTitle(title)
               .SetContentText(body)
               .SetStyle(new Notification.BigTextStyle().BigText(body))
               .SetSmallIcon(Resource.Drawable.ic_notification)
               .SetAutoCancel(true);
        if (pending is not null) builder.SetContentIntent(pending);

        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        manager?.Notify(System.Environment.TickCount, builder.Build());
    }
}

/// <summary>Firebase token for this installation (IPushTokenProvider for Core).</summary>
public sealed class FirebaseTokenProvider : IPushTokenProvider
{
    public string Platform => "android";

    public Task<string?> GetTokenAsync()
    {
        var tcs = new TaskCompletionSource<string?>();
        try
        {
            FirebaseMessaging.Instance.GetToken()
                .AddOnSuccessListener(new Listener(r => tcs.TrySetResult(r?.ToString())))
                .AddOnFailureListener(new Listener(_ => tcs.TrySetResult(null)));
        }
        catch
        {
            // Firebase not configured (no google-services.json): no push, in-app list still works.
            tcs.TrySetResult(null);
        }
        return tcs.Task;
    }

    private sealed class Listener : Java.Lang.Object, IOnSuccessListener, IOnFailureListener
    {
        private readonly Action<Java.Lang.Object?> _action;
        public Listener(Action<Java.Lang.Object?> action) => _action = action;
        public void OnSuccess(Java.Lang.Object? result) => _action(result);
        public void OnFailure(Java.Lang.Exception e) => _action(null);
    }
}

/// <summary>Receives pushes and token rotations from Firebase.</summary>
[Service(Exported = false)]
[IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
public class AthloTrackMessagingService : FirebaseMessagingService
{
    public override void OnNewToken(string token)
    {
        base.OnNewToken(token);
        try
        {
            // Only relevant while signed in; otherwise the next sign-in registers the token.
            _ = App.Services?.GetService<PushRegistrationService>()?.RegisterTokenAsync(token);
        }
        catch
        {
            // app not initialised yet
        }
    }

    public override void OnMessageReceived(RemoteMessage message)
    {
        base.OnMessageReceived(message);
        var n = message.GetNotification();
        PushNotifications.Show(this, n?.Title ?? "AthloTrack", n?.Body ?? string.Empty);
    }
}
