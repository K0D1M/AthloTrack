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
    private static Task Main(string[] args)
    {
        AppBootstrap.RegisterPlatformServices = services =>
            services.AddSingleton<ICredentialStore, BrowserCredentialStore>();

        return BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}