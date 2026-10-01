using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using AthloTrack.Core.Push;

namespace AthloTrack.Browser.Services;

/// <summary>wwwroot/push.js (Firebase Cloud Messaging for the web), imported as module "push".</summary>
[SupportedOSPlatform("browser")]
internal static partial class PushInterop
{
    public static bool Loaded { get; private set; }

    /// <summary>firebase-config.js has the web app's Firebase values; without them web push stays off.</summary>
    public static bool Configured => Loaded && State() != "unconfigured";

    public static async Task LoadAsync()
    {
        try
        {
            await JSHost.ImportAsync("push", "/push.js");
            Loaded = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AthloTrack] Web push unavailable: {ex.Message}");
        }
    }

    [JSImport("state", "push")]
    public static partial string State();

    [JSImport("requestPermission", "push")]
    public static partial Task<bool> RequestPermission();

    [JSImport("getToken", "push")]
    public static partial Task<string> GetToken();

    [JSImport("launchRequest", "push")]
    public static partial string LaunchRequest();
}

/// <summary>This browser's push token (Firebase web), registered as platform "web".</summary>
[SupportedOSPlatform("browser")]
public sealed class BrowserPushTokenProvider : IPushTokenProvider, IPushPermission
{
    public string Platform => "web";

    public PushPermission State => !PushInterop.Loaded ? PushPermission.Unsupported : PushInterop.State() switch
    {
        "granted" => PushPermission.Granted,
        "denied" => PushPermission.Denied,
        "not-asked" => PushPermission.NotAsked,
        "install-first" => PushPermission.InstallFirst,
        _ => PushPermission.Unsupported,
    };

    public async Task<bool> RequestAsync() => PushInterop.Loaded && await PushInterop.RequestPermission();

    public async Task<string?> GetTokenAsync()
    {
        if (!PushInterop.Loaded) return null;
        try
        {
            var token = await PushInterop.GetToken();
            return string.IsNullOrEmpty(token) ? null : token;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AthloTrack] Web push token failed: {ex.Message}");
            return null;
        }
    }
}
