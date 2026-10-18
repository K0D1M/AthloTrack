using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using AthloTrack.Android.Services;
using AthloTrack.Core.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace AthloTrack.Android
{
    [Application]
    public class Application : AvaloniaAndroidApplication<App>
    {
        protected Application(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            AthloTrack.AppBootstrap.RegisterPlatformServices = services =>
            {
                services.AddSingleton<ICredentialStore, AndroidCredentialStore>();
                services.AddSingleton<IAppPreferences, AndroidAppPreferences>();
                services.AddSingleton<AthloTrack.Core.Push.IPushTokenProvider, FirebaseTokenProvider>();
            };
            PushNotifications.EnsureChannel(this);
            // «Νέα έκδοση»: this APK reads the "android" entry of athlotrack.gr/version/latest.json.
            AthloTrack.Services.UpdateChecker.Channel = "android";

            return base.CustomizeAppBuilder(builder)
            .WithInterFont();
        }
    }
}
