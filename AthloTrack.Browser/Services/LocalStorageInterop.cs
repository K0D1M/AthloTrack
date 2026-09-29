using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace AthloTrack.Browser.Services;

[SupportedOSPlatform("browser")]
internal static partial class LocalStorageInterop
{
    [JSImport("globalThis.localStorage.setItem")]
    public static partial void SetItem(string key, string value);

    [JSImport("globalThis.localStorage.getItem")]
    public static partial string? GetItem(string key);

    [JSImport("globalThis.localStorage.removeItem")]
    public static partial void RemoveItem(string key);
}
