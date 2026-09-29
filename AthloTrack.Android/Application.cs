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
                services.AddSingleton<ICredentialStore, AndroidCredentialStore>();

            return base.CustomizeAppBuilder(builder)
            .WithInterFont();
        }
    }
}
