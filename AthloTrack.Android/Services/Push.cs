using System;
using System.Collections.Generic;
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
        // Channels exist from Android 8 (API 26); this form of the check is what the analyzer understands.
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;
        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        manager?.CreateNotificationChannel(new NotificationChannel(ChannelId, "AthloTrack", NotificationImportance.High)
        {
            Description = "Νέα ασκησιολόγια και ολοκληρωμένες προπονήσεις",
        });
    }

    // AthloTrack's own notification group. Without it Android bundles 4+ notifications into an
    // automatic group whose tap only opens the app; our summary opens Προπονήσεις instead.
    private const string GroupKey = "athlotrack_workouts";
    private const int SummaryId = 1;
    private const string ExtraNotificationId = "athlo_notification_id";
    private const string ExtraType = "athlo_type";
    private const string ExtraBody = "athlo_body";

    /// <summary>Posts a push (always: the app receives data-only messages, also in the background).</summary>
    public static void Show(Context context, string title, string body, IDictionary<string, string>? data = null)
    {
        EnsureChannel(context);
        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (manager is null) return;

        var id = System.Environment.TickCount & int.MaxValue;
        if (id == SummaryId) id++;
        string? notificationId = null, type = null;
        data?.TryGetValue("notificationId", out notificationId);
        data?.TryGetValue("type", out type);

        var extras = new Bundle();
        extras.PutString(ExtraNotificationId, notificationId);
        extras.PutString(ExtraType, type);
        extras.PutString(ExtraBody, body);

        var builder = NewBuilder(context)
            .SetContentTitle(title)
            .SetContentText(body)
            .SetStyle(new Notification.BigTextStyle().BigText(body))
            .SetSmallIcon(Resource.Drawable.ic_notification)
            .SetAutoCancel(true)
            .SetGroup(GroupKey)
            .AddExtras(extras);
        if (Launch(context, id, ("type", type), ("notificationId", notificationId)) is { } pending)
            builder.SetContentIntent(pending);
        manager.Notify(id, builder.Build());

        UpdateSummary(context, manager);
    }

    /// <summary>2+ AthloTrack notifications: a summary that opens Προπονήσεις and marks them all read.</summary>
    private static void UpdateSummary(Context context, NotificationManager manager)
    {
        var children = new List<Notification>();
        foreach (var active in manager.GetActiveNotifications() ?? Array.Empty<global::Android.Service.Notification.StatusBarNotification>())
        {
            var n = active.Notification;
            if (n is not null && n.Group == GroupKey && (n.Flags & NotificationFlags.GroupSummary) == 0) children.Add(n);
        }
        if (children.Count < 2) return;

        var ids = new List<string>();
        string? type = null;
        var style = new Notification.InboxStyle();
        foreach (var n in children)
        {
            if (n.Extras?.GetString(ExtraNotificationId) is { Length: > 0 } nid) ids.Add(nid);
            type ??= n.Extras?.GetString(ExtraType);
            if (n.Extras?.GetString(ExtraBody) is { } line) style.AddLine(line);
        }
        style.SetSummaryText($"{children.Count} ειδοποιήσεις");

        var summary = NewBuilder(context)
            .SetContentTitle("AthloTrack")
            .SetContentText($"{children.Count} νέες ειδοποιήσεις")
            .SetStyle(style)
            .SetSmallIcon(Resource.Drawable.ic_notification)
            .SetAutoCancel(true)
            .SetGroup(GroupKey)
            .SetGroupSummary(true);
        if (Launch(context, SummaryId, ("type", type ?? "new_workout"), ("notificationId", string.Join(",", ids)), ("clearAll", "1")) is { } pending)
            summary.SetContentIntent(pending);
        manager.Notify(SummaryId, summary.Build());
    }

    private static Notification.Builder NewBuilder(Context context)
    {
#pragma warning disable CA1422 // pre-26 fallback constructor
        return OperatingSystem.IsAndroidVersionAtLeast(26)
            ? new Notification.Builder(context, ChannelId)
            : new Notification.Builder(context);
#pragma warning restore CA1422
    }

    /// <summary>Opens the app with these extras (MainActivity turns them into the section to open).</summary>
    private static PendingIntent? Launch(Context context, int requestCode, params (string Key, string? Value)[] extras)
    {
        var launch = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!);
        if (launch is null) return null;
        launch.AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        foreach (var (key, value) in extras)
            if (!string.IsNullOrEmpty(value)) launch.PutExtra(key, value);
        // Unique request code: otherwise one notification's extras would replace another's.
        return PendingIntent.GetActivity(context, requestCode, launch, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
    }

    /// <summary>The summary was tapped: everything in the group has been seen.</summary>
    public static void ClearAll(Context context) =>
        ((NotificationManager?)context.GetSystemService(Context.NotificationService))?.CancelAll();
}

/// <summary>Firebase token for this installation (IPushTokenProvider for Core).</summary>
public sealed class FirebaseTokenProvider : IPushTokenProvider
{
    // "android-data": app 1.2+, which posts its own notifications, so the push function sends it
    // data-only messages. Older installs registered as "android" still get notification messages.
    public string Platform => "android-data";

    public Task<string?> GetTokenAsync()
    {
        var tcs = new TaskCompletionSource<string?>();
        try
        {
            // GetToken is Firebase's current API; the .NET binding marks it [Obsolete("deprecated")] by mistake.
#pragma warning disable CS0618
            FirebaseMessaging.Instance.GetToken()
#pragma warning restore CS0618
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
    // OnNewToken is Firebase's current callback; the .NET binding marks it [Obsolete("deprecated")] by mistake.
#pragma warning disable CS0618, CS0672
    public override void OnNewToken(string token)
    {
        base.OnNewToken(token);
#pragma warning restore CS0618, CS0672
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
        // Data-only messages (current function) carry title/body in data; notification messages
        // (sent to old installs) only reach here while the app is open.
        var n = message.GetNotification();
        var data = message.Data;
        string? title = null, body = null;
        data?.TryGetValue("title", out title);
        data?.TryGetValue("body", out body);
        PushNotifications.Show(this, n?.Title ?? title ?? "AthloTrack", n?.Body ?? body ?? string.Empty, data);
    }
}
