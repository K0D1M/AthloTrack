using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AthloTrack.Core.Data;
using AthloTrack.Core.Models.Admin;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

public sealed class AdminDeviceItem
{
    public AdminDeviceItem(DeviceInfo device)
    {
        Device = device;
    }

    public DeviceInfo Device { get; }
    public string Name => Device.Name;
    public string Initial => string.IsNullOrEmpty(Device.Name) ? "?" : Device.Name[..1].ToUpperInvariant();
    public string Summary =>
        $"{AdminText.Role(Device.Role)} · {AdminText.Platform(Device.Platform)} · ενημέρωση {AdminText.Ago(Device.UpdatedAt)}";
}

/// <summary>Ειδοποιήσεις (admin): who has the app, a test push per user, and announcements.</summary>
public partial class AdminPushViewModel : ViewModelBase
{
    private readonly IAdminRepository _admin;
    private bool _hasLoaded;

    public AdminPushViewModel(IAdminRepository admin)
    {
        _admin = admin;
        _ = LoadAsync();
    }

    public ObservableCollection<AdminDeviceItem> Devices { get; } = new();
    public ConfirmPrompt Confirm { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSkeleton), nameof(IsEmpty))]
    public partial bool IsLoading { get; set; }

    public bool ShowSkeleton => IsLoading && !_hasLoaded;
    public bool IsEmpty => !IsLoading && Devices.Count == 0 && ErrorMessage is null;

    [ObservableProperty]
    public partial bool IsWorking { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    // ---- Announcement ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToAll), nameof(ToCoaches), nameof(ToAthletes))]
    public partial BroadcastAudience Audience { get; set; }

    public bool ToAll { get => Audience == BroadcastAudience.All; set { if (value) Audience = BroadcastAudience.All; } }
    public bool ToCoaches { get => Audience == BroadcastAudience.Coaches; set { if (value) Audience = BroadcastAudience.Coaches; } }
    public bool ToAthletes { get => Audience == BroadcastAudience.Athletes; set { if (value) Audience = BroadcastAudience.Athletes; } }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Body { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? FormError { get; set; }

    [RelayCommand]
    private void Send()
    {
        var body = Body.Trim();
        FormError = body.Length == 0 ? "Γράψε το κείμενο της ανακοίνωσης." : null;
        if (FormError is not null) return;

        var who = Audience switch
        {
            BroadcastAudience.Coaches => "σε όλους τους προπονητές",
            BroadcastAudience.Athletes => "σε όλους τους αθλητές",
            _ => "σε όλους",
        };
        Confirm.Ask($"Αποστολή της ανακοίνωσης {who} που έχουν την εφαρμογή;", "Αποστολή", () => RunAsync(async () =>
        {
            var result = await _admin.BroadcastAsync(Audience, Title.Trim(), body);
            StatusMessage = $"Στάλθηκε σε {result.Sent} {(result.Sent == 1 ? "συσκευή" : "συσκευές")} ({result.Users} χρήστες).";
            Title = string.Empty;
            Body = string.Empty;
        }));
    }

    [RelayCommand]
    private Task TestPushAsync(AdminDeviceItem? item) => item is null
        ? Task.CompletedTask
        : RunAsync(async () =>
        {
            var result = await _admin.SendTestPushAsync(item.Device.AuthUserId);
            StatusMessage = result.Sent > 0
                ? $"Η δοκιμαστική ειδοποίηση στάλθηκε στον/στην {item.Name} ({result.Sent} {(result.Sent == 1 ? "συσκευή" : "συσκευές")})."
                : $"Δεν στάλθηκε: ο/η {item.Name} δεν έχει πια ενεργή συσκευή.";
            await LoadAsync();
        });

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var devices = await _admin.GetDevicesAsync();
            Devices.Clear();
            foreach (var d in devices) Devices.Add(new AdminDeviceItem(d));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            _hasLoaded = true;
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    private async Task RunAsync(Func<Task> action)
    {
        IsWorking = true;
        ErrorMessage = null;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsWorking = false;
        }
    }
}
