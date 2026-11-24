using System;
using System.IO;
using AthloTrack.Core.Offline;

namespace AthloTrack.Android.Services;

/// <summary>
/// Offline copies (Προπονήσεις, Ημερολόγιο, the queued answers) as files in the app's private
/// storage, which other apps can't read. Sign-out deletes them.
/// </summary>
public sealed class AndroidOfflineStore : IOfflineStore
{
    private static string Root => global::Android.App.Application.Context.FilesDir!.AbsolutePath;

    /// <summary>«offline/abc/workouts» → files/offline/abc/workouts.json (keys are our own, never user input).</summary>
    private static string PathOf(string key) =>
        Path.Combine(Root, key.Replace('/', Path.DirectorySeparatorChar) + ".json");

    public string? Read(string key)
    {
        var path = PathOf(key);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public void Write(string key, string text)
    {
        var path = PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Write then swap, so a phone switched off mid-write never leaves half a copy.
        var temp = path + ".tmp";
        File.WriteAllText(temp, text);
        File.Move(temp, path, overwrite: true);
    }

    public void Delete(string key)
    {
        var path = PathOf(key);
        if (File.Exists(path)) File.Delete(path);
    }

    public void DeleteAll(string prefix)
    {
        // Our prefixes are folders («offline/»): remove the whole folder.
        var dir = Path.Combine(Root, prefix.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AthloTrack] offline copies not deleted: {ex.Message}");
        }
    }
}
