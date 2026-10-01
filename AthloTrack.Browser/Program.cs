using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using AthloTrack;
using AthloTrack.Browser.Services;
using AthloTrack.Core.Auth;
using Microsoft.Extensions.DependencyInjection;

internal sealed partial class Program
{
    private static async Task Main(string[] args)
    {
        await PushInterop.LoadAsync();
        // Opened by tapping a notification (?type=…&notification=…): open Προπονήσεις once signed in.
        if (PushInterop.Loaded && PushInterop.LaunchRequest() is { Length: > 0 } request)
        {
            var parts = request.Split('|');
            AthloTrack.Services.NotificationNavigation.RequestFor(parts[0], parts.Length > 1 ? parts[1] : null);
        }

        AppBootstrap.RegisterPlatformServices = services =>
        {
            services.AddSingleton<ICredentialStore, BrowserCredentialStore>();
            // Only once Firebase is configured; otherwise the app shows no notification options at all.
            if (PushInterop.Configured)
                services.AddSingleton<AthloTrack.Core.Push.IPushTokenProvider, BrowserPushTokenProvider>();
        };

        await BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}
