using System.Runtime.Versioning;
using AthloTrack.Core.Auth;

namespace AthloTrack.Browser.Services;

/// <summary>App preferences in localStorage, next to the session.</summary>
[SupportedOSPlatform("browser")]
public sealed class BrowserAppPreferences : IAppPreferences
{
    private const string Prefix = "athlotrack.pref.";

    public string? Get(string key) => LocalStorageInterop.GetItem(Prefix + key);

    public void Set(string key, string? value)
    {
        if (value is null) LocalStorageInterop.RemoveItem(Prefix + key);
        else LocalStorageInterop.SetItem(Prefix + key, value);
    }
}
