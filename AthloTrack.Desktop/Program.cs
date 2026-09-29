using System;
using Avalonia;
using AthloTrack.Core.Auth;
using AthloTrack.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AthloTrack.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        AthloTrack.AppBootstrap.RegisterPlatformServices = services =>
            services.AddSingleton<ICredentialStore, DesktopCredentialStore>();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
