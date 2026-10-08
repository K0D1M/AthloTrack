using System;

namespace AthloTrack.Services;

/// <summary>
/// A tapped photo, shown large over the whole app by the shell (MainView) until tapped again.
/// </summary>
public static class PhotoPreview
{
    /// <summary>The shell shows the photo (JPEG/PNG bytes).</summary>
    public static event Action<byte[]>? Requested;

    public static void Show(byte[] photo) => Requested?.Invoke(photo);
}
