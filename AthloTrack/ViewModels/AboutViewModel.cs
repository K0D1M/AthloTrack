using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AthloTrack.ViewModels;

/// <summary>Σχετικά — about page.</summary>
public partial class AboutViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string AppName { get; set; } = "AthloTrack";

    [ObservableProperty]
    public partial string Version { get; set; } = AppVersion();

    /// <summary>The <c>Version</c> from AthloTrack.csproj, without the "+commit" suffix the SDK appends.</summary>
    static string AppVersion()
    {
        var v = typeof(AboutViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        var plus = v.IndexOf('+');
        return plus >= 0 ? v[..plus] : v;
    }
}
