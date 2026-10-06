using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AthloTrack.ViewModels;

/// <summary>One open-source component or font credited in Σχετικά.</summary>
public sealed record CreditItem(string Name, string Use, string License);

/// <summary>Σχετικά — about page.</summary>
public partial class AboutViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string AppName { get; set; } = "AthloTrack";

    [ObservableProperty]
    public partial string Version { get; set; } = AppInfo.Version;

    public bool IsPreRelease => AppInfo.IsPreRelease;

    public string Copyright { get; } = "© 2026 K0D1M";

    /// <summary>What the app is built with, and under which licence each part is used.</summary>
    public IReadOnlyList<CreditItem> Credits { get; } =
    [
        new(".NET", "πλατφόρμα εφαρμογής", "MIT"),
        new("Avalonia UI", "περιβάλλον εφαρμογής", "MIT"),
        new("CommunityToolkit.Mvvm", "δομή εφαρμογής", "MIT"),
        new("ScottPlot", "γραφήματα προόδου", "MIT"),
        new("SkiaSharp", "σχεδίαση", "MIT"),
        new("Supabase", "λογαριασμοί και αποθήκευση δεδομένων", "MIT"),
        new("Firebase Cloud Messaging", "ειδοποιήσεις στο Android", "Apache 2.0"),
        new("Inter", "γραμματοσειρά", "SIL OFL 1.1"),
        new("GFS Didot (Greek Font Society)", "ελληνική γραμματοσειρά", "SIL OFL 1.1"),
    ];
}
