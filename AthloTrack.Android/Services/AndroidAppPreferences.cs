using Android.Content;
using AthloTrack.Core.Auth;

namespace AthloTrack.Android.Services;

/// <summary>App preferences in the app's private SharedPreferences.</summary>
public sealed class AndroidAppPreferences : IAppPreferences
{
    private readonly ISharedPreferences _prefs =
        global::Android.App.Application.Context.GetSharedPreferences("athlotrack", FileCreationMode.Private)!;

    public string? Get(string key) => _prefs.GetString(key, null);

    public void Set(string key, string? value)
    {
        using var editor = _prefs.Edit()!;
        if (value is null) editor.Remove(key);
        else editor.PutString(key, value);
        editor.Apply();
    }
}
