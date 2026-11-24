using System.Net.Http;

namespace AthloTrack.Core.Offline;

/// <summary>
/// Whether the app is showing saved copies because there is no internet, and since when they
/// were saved. The shell shows a banner from it; screens reload when <see cref="Reconnected"/>.
/// </summary>
public sealed class OfflineState
{
    private bool _isOffline;

    public bool IsOffline => _isOffline;

    /// <summary>When the copies on screen were last loaded from the server.</summary>
    public DateTimeOffset? SavedAt { get; private set; }

    /// <summary>Answers given offline, waiting to be sent.</summary>
    public int PendingAnswers { get; private set; }

    /// <summary>Raised (on whatever thread) when anything above changes.</summary>
    public event Action? Changed;

    /// <summary>The internet is back: the shell reloads the page on screen.</summary>
    public event Action? Reconnected;

    /// <summary>A queued answer the server refused once back online (e.g. the workout was cancelled).</summary>
    public event Action<string>? Notice;

    public void SetOffline(DateTimeOffset? savedAt)
    {
        var changed = !_isOffline || SavedAt != savedAt;
        _isOffline = true;
        if (savedAt is not null) SavedAt = savedAt;
        if (changed) Changed?.Invoke();
    }

    public void SetOnline()
    {
        if (!_isOffline) return;
        _isOffline = false;
        Changed?.Invoke();
        Reconnected?.Invoke();
    }

    public void SetPending(int count)
    {
        if (PendingAnswers == count) return;
        PendingAnswers = count;
        Changed?.Invoke();
    }

    public void Tell(string message) => Notice?.Invoke(message);

    /// <summary>
    /// The request never reached the server (no network, DNS, timeout). Server answers (RLS,
    /// constraint, 4xx/5xx with a body) are not network errors.
    /// </summary>
    public static bool IsNetworkError(Exception? ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is HttpRequestException { StatusCode: null } or TaskCanceledException or TimeoutException
                or System.Net.Sockets.SocketException or System.IO.IOException)
                return true;
            if (e is OfflineException) return true;
        }
        return false;
    }
}

/// <summary>A request that needs the internet was tried without it. The message is for the user.</summary>
public sealed class OfflineException(string message, Exception? inner = null) : Exception(message, inner)
{
    public const string NeedsInternet = "Χρειάζεται σύνδεση στο internet.";
    public const string NoInternet = "Χωρίς σύνδεση στο internet.";
}
