using System;
using System.IO;
using AthloTrack.Core.Auth;

namespace AthloTrack.Desktop.Services;

/// <summary>App preferences as one small text file per key in %LOCALAPPDATA%\AthloTrack\prefs.</summary>
public sealed class DesktopAppPreferences : IAppPreferences
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AthloTrack", "prefs");

    public string? Get(string key)
    {
        var path = Path.Combine(Folder, key);
        try { return File.Exists(path) ? File.ReadAllText(path) : null; }
        catch (IOException) { return null; }
    }

    public void Set(string key, string? value)
    {
        var path = Path.Combine(Folder, key);
        try
        {
            if (value is null) File.Delete(path);
            else
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(path, value);
            }
        }
        catch (IOException)
        {
            // Only a convenience: the login screen just won't preselect a role.
        }
    }
}
