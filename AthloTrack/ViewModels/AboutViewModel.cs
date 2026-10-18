using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AthloTrack.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

    // ---- «Έλεγχος για ενημέρωση» (the Android app only) ----

    public bool CanCheckUpdates => UpdateChecker.IsSupported;

    [ObservableProperty]
    public partial string? UpdateStatus { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdate))]
    public partial UpdateInfo? Update { get; set; }

    public bool HasUpdate => Update is not null;

    [ObservableProperty]
    public partial bool IsChecking { get; set; }

    [RelayCommand]
    private async Task CheckUpdateAsync()
    {
        IsChecking = true;
        UpdateStatus = null;
        Update = null;
        try
        {
            Update = await UpdateChecker.CheckAsync(throwOnError: true);
            UpdateStatus = Update is { } u
                ? $"Νέα έκδοση {u.Version} διαθέσιμη."
                : "Έχεις την τελευταία έκδοση.";
        }
        catch (Exception)
        {
            UpdateStatus = "Δεν ήταν δυνατός ο έλεγχος. Έλεγξε τη σύνδεση και δοκίμασε ξανά.";
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand]
    private Task DownloadAsync() => Update is { } u ? UpdateChecker.OpenAsync(u) : Task.CompletedTask;

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
