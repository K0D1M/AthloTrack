using System.Runtime.Versioning;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;

namespace AthloTrack.Browser.Services;

/// <summary>
/// Browser session storage backed by localStorage. Note: this is plaintext in the browser's
/// storage — an acceptable simplicity tradeoff for a tiny, trusted internal user base.
/// Harden later with WebCrypto + IndexedDB if needed.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class BrowserCredentialStore : ICredentialStore
{
    private const string AccessKey = "athlotrack.access_token";
    private const string RefreshKey = "athlotrack.refresh_token";

    public Task SaveSessionAsync(string accessToken, string refreshToken)
    {
        LocalStorageInterop.SetItem(AccessKey, accessToken);
        LocalStorageInterop.SetItem(RefreshKey, refreshToken);
        return Task.CompletedTask;
    }

    public Task<(string? AccessToken, string? RefreshToken)> LoadSessionAsync()
    {
        var access = LocalStorageInterop.GetItem(AccessKey);
        var refresh = LocalStorageInterop.GetItem(RefreshKey);
        return Task.FromResult((access, refresh));
    }

    public Task ClearAsync()
    {
        LocalStorageInterop.RemoveItem(AccessKey);
        LocalStorageInterop.RemoveItem(RefreshKey);
        return Task.CompletedTask;
    }
}
