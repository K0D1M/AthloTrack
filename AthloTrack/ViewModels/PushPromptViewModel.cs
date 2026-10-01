using System;
using System.Threading.Tasks;
using AthloTrack.Core.Push;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

/// <summary>
/// "Turn on notifications" for heads where the user must allow them with a tap (the web app,
/// including iPhone from the Home Screen). Shown in Ρυθμίσεις and, until enabled, on Πρόσφατα.
/// </summary>
public partial class PushPromptViewModel : ViewModelBase
{
    private readonly PushRegistrationService _push;

    public PushPromptViewModel(PushRegistrationService push)
    {
        _push = push;
        Refresh();
    }

    /// <summary>False on heads without a permission step (Android asks itself; desktop has no push).</summary>
    public bool IsAvailable => _push.Permission is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(CanEnable), nameof(ShowBanner), nameof(IsOn))]
    public partial PushPermission State { get; set; } = PushPermission.Unsupported;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial string? ErrorMessage { get; set; }

    public bool CanEnable => IsAvailable && State == PushPermission.NotAsked;
    public bool IsOn => State == PushPermission.Granted;

    /// <summary>Πρόσφατα: only while there is something the user can do.</summary>
    public bool ShowBanner => IsAvailable && State is PushPermission.NotAsked or PushPermission.InstallFirst;

    public string StatusText => ErrorMessage ?? State switch
    {
        PushPermission.Granted => "✓ Οι ειδοποιήσεις είναι ενεργές σε αυτή τη συσκευή.",
        PushPermission.NotAsked => "Λάβε ειδοποίηση για νέα και ολοκληρωμένα ασκησιολόγια, ακόμη κι όταν η εφαρμογή είναι κλειστή.",
        PushPermission.InstallFirst =>
            "Στο iPhone: πάτησε Κοινοποίηση (□↑) → «Προσθήκη στην οθόνη Αφετηρίας», άνοιξε το AthloTrack από το εικονίδιο και ενεργοποίησε εδώ τις ειδοποιήσεις.",
        PushPermission.Denied => "Οι ειδοποιήσεις είναι μπλοκαρισμένες. Επίτρεψέ τες από τις ρυθμίσεις του browser για αυτή τη σελίδα.",
        _ => "Αυτός ο browser δεν υποστηρίζει ειδοποιήσεις.",
    };

    public void Refresh() => State = _push.Permission?.State ?? PushPermission.Unsupported;

    [RelayCommand]
    private async Task EnableAsync()
    {
        ErrorMessage = null;
        try
        {
            if (!await _push.EnableAsync() && _push.Permission?.State == PushPermission.Granted)
                ErrorMessage = "Οι ειδοποιήσεις επιτράπηκαν, αλλά η σύνδεση με τον διακομιστή απέτυχε. Δοκίμασε ξανά αργότερα.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        Refresh();
    }
}
