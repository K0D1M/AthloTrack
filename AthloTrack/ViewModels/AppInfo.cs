using System.Reflection;

namespace AthloTrack.ViewModels;

/// <summary>The app version from <c>&lt;Version&gt;</c> in AthloTrack.csproj, e.g. "1.10-alpha".</summary>
public static class AppInfo
{
    /// <summary>The version without the "+commit" suffix the SDK appends.</summary>
    public static string Version { get; } = ReadVersion();

    /// <summary>The release stage after the dash ("alpha", "beta"), or "" for a stable release.</summary>
    public static string Stage { get; } = Version.Contains('-') ? Version[(Version.IndexOf('-') + 1)..] : "";

    public static bool IsPreRelease => Stage.Length > 0;

    static string ReadVersion()
    {
        var v = typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        var plus = v.IndexOf('+');
        return plus >= 0 ? v[..plus] : v;
    }
}
