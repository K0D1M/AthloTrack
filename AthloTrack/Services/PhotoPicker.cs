using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace AthloTrack.Services;

/// <summary>Opens the platform file picker and returns a ready-to-upload avatar JPEG.</summary>
public static class PhotoPicker
{
    public sealed record Result(byte[]? Jpeg, string? Error);

    /// <returns>null when the user cancelled; otherwise the image or an error message.</returns>
    public static async Task<Result?> PickAvatarAsync(Visual anchor)
    {
        var storage = TopLevel.GetTopLevel(anchor)?.StorageProvider;
        if (storage is null || !storage.CanOpen) return new Result(null, "Η επιλογή αρχείου δεν υποστηρίζεται εδώ.");

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Επιλογή φωτογραφίας",
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.ImageAll },
        });
        var file = files.FirstOrDefault();
        if (file is null) return null;

        // Copy first: browser streams aren't seekable, and the decoder needs to seek.
        await using var input = await file.OpenReadAsync();
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer);

        var jpeg = PhotoProcessor.ToAvatarJpeg(buffer.ToArray());
        return jpeg is null
            ? new Result(null, "Το αρχείο δεν είναι εικόνα που μπορεί να διαβαστεί.")
            : new Result(jpeg, null);
    }
}
