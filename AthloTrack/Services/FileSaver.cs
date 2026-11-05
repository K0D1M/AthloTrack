using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace AthloTrack.Services;

/// <summary>
/// Saves a file the app made (a form's PDF) with the platform's own save dialog: Android's file
/// chooser, Windows' «Αποθήκευση ως», the browser's. Browsers without a save dialog (Firefox,
/// Safari) get a plain download instead, through <see cref="BrowserDownload"/>.
/// </summary>
public static class FileSaver
{
    /// <summary>Set by the web head: (file name, media type, bytes) → a download.</summary>
    public static Func<string, string, byte[], Task>? BrowserDownload { get; set; }

    /// <summary>Returns the saved file's name, or null when the dialog was cancelled.</summary>
    public static async Task<string?> SaveAsync(TopLevel top, string fileName, string mediaType, byte[] bytes)
    {
        if (!top.StorageProvider.CanSave && BrowserDownload is { } download)
        {
            await download(fileName, mediaType, bytes);
            return fileName;
        }

        var extension = System.IO.Path.GetExtension(fileName).TrimStart('.');
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            // Without the extension: Android's chooser adds DefaultExtension itself («….pdf.pdf»).
            SuggestedFileName = System.IO.Path.GetFileNameWithoutExtension(fileName),
            DefaultExtension = extension,
            ShowOverwritePrompt = true,
            FileTypeChoices = [new FilePickerFileType(extension.ToUpperInvariant()) { Patterns = [$"*.{extension}"], MimeTypes = [mediaType] }],
        });
        if (file is null) return null;
        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(bytes);
        return file.Name;
    }
}
