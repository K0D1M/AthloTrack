using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AthloTrack.Core.Data;
using AthloTrack.Core.Models.Admin;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

public sealed class AdminCoachItem
{
    public AdminCoachItem(AdminCoach coach)
    {
        Coach = coach;
    }

    public AdminCoach Coach { get; }
    public string FullName => Coach.FullName;
    public string Email => Coach.Email ?? "—";
    public string Initial => string.IsNullOrEmpty(Coach.FullName) ? "?" : Coach.FullName[..1].ToUpperInvariant();
    public string Summary =>
        $"{Coach.Athletes} {(Coach.Athletes == 1 ? "αθλητής" : "αθλητές")} · Σύνδεση: {AdminText.Ago(Coach.LastSignInAt)}";
    public bool PendingPassword => Coach.MustSetPassword;
    public bool HasApp => Coach.HasApp;
    public bool HasLogin => Coach.AuthUserId is not null;
}

/// <summary>A temporary password, shown once after it was created (the app never stores it).</summary>
public sealed record IssuedPassword(string For, string Email, string Password);

/// <summary>Προπονητές (admin): all coaches, new coach, passwords, delete.</summary>
public partial class AdminCoachesViewModel : ViewModelBase
{
    private readonly IAdminRepository _admin;
    private List<AdminCoachItem> _all = new();
    private bool _hasLoaded;

    public AdminCoachesViewModel(IAdminRepository admin)
    {
        _admin = admin;
        _ = LoadAsync();
    }

    public ObservableCollection<AdminCoachItem> Coaches { get; } = new();
    public ConfirmPrompt Confirm { get; } = new();

    [ObservableProperty]
    public partial string Search { get; set; } = string.Empty;

    partial void OnSearchChanged(string value) => ApplyFilter();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSkeleton), nameof(IsEmpty))]
    public partial bool IsLoading { get; set; }

    public bool ShowSkeleton => IsLoading && !_hasLoaded;
    public bool IsEmpty => !IsLoading && Coaches.Count == 0 && ErrorMessage is null;

    /// <summary>A server action is running (buttons disabled, spinner).</summary>
    [ObservableProperty]
    public partial bool IsWorking { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIssuedPassword))]
    public partial IssuedPassword? Issued { get; set; }

    public bool HasIssuedPassword => Issued is not null;

    // ---- New coach (inline form) ----

    [ObservableProperty]
    public partial bool IsCreating { get; set; }

    [ObservableProperty]
    public partial string NewName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewEmail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? FormError { get; set; }

    [RelayCommand]
    private void StartCreate()
    {
        NewName = string.Empty;
        NewEmail = string.Empty;
        FormError = null;
        IsCreating = true;
    }

    [RelayCommand]
    private void CancelCreate() => IsCreating = false;

    [RelayCommand]
    public async Task CreateAsync()
    {
        var name = NewName.Trim();
        var email = NewEmail.Trim();
        FormError = string.IsNullOrEmpty(name) ? "Γράψε το ονοματεπώνυμο."
            : !IsEmail(email) ? "Γράψε ένα έγκυρο email."
            : _all.Any(c => string.Equals(c.Coach.Email, email, StringComparison.OrdinalIgnoreCase))
                ? "Υπάρχει ήδη προπονητής με αυτό το email."
            : null;
        if (FormError is not null) return;

        await RunAsync(async () =>
        {
            var result = await _admin.CreateCoachAsync(email, name);
            IsCreating = false;
            Issued = new IssuedPassword(name, email, result.TempPassword ?? "");
            StatusMessage = $"Δημιουργήθηκε ο λογαριασμός για {name}.";
            await LoadAsync();
        });
    }

    // ---- Per coach ----

    [RelayCommand]
    private void ResetPassword(AdminCoachItem? item)
    {
        if (item?.Coach.AuthUserId is not { } userId) return;
        Confirm.Ask($"Νέος προσωρινός κωδικός για {item.FullName}; Ο τωρινός κωδικός παύει να ισχύει.",
            "Νέος κωδικός", () => RunAsync(async () =>
            {
                var result = await _admin.ResetPasswordAsync(userId);
                Issued = new IssuedPassword(item.FullName, item.Email, result.TempPassword ?? "");
                StatusMessage = null;
                await LoadAsync();
            }));
    }

    [RelayCommand]
    private void ForcePasswordChange(AdminCoachItem? item)
    {
        if (item is null) return;
        Confirm.Ask($"Στην επόμενη σύνδεση ο/η {item.FullName} θα πρέπει να ορίσει νέο κωδικό.",
            "Επιβολή", () => RunAsync(async () =>
            {
                await _admin.ForcePasswordChangeAsync(item.Coach.Id);
                StatusMessage = $"Ο/Η {item.FullName} θα ορίσει νέο κωδικό στην επόμενη σύνδεση.";
                await LoadAsync();
            }));
    }

    [RelayCommand]
    private void Delete(AdminCoachItem? item)
    {
        if (item?.Coach.AuthUserId is not { } userId) return;
        var athletes = item.Coach.Athletes;
        var warning = athletes == 0
            ? "Ο λογαριασμός και το προφίλ διαγράφονται οριστικά."
            : $"Διαγράφονται οριστικά ο λογαριασμός, το προφίλ και οι {athletes} αθλητές του/της, με όλες τις μετρήσεις και τα ασκησιολόγιά τους.";
        Confirm.Ask($"Διαγραφή του/της {item.FullName}; {warning}", "Διαγραφή", () => RunAsync(async () =>
        {
            await _admin.DeleteLoginAsync(userId);
            StatusMessage = $"Διαγράφηκε: {item.FullName}.";
            await LoadAsync();
        }), danger: true, requiredText: item.Coach.Email ?? item.FullName);
    }

    [RelayCommand]
    private void DismissIssued() => Issued = null;

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            _all = (await _admin.GetCoachesAsync()).Select(c => new AdminCoachItem(c)).ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            _hasLoaded = true;
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        var q = Search.Trim();
        Coaches.Clear();
        foreach (var c in _all)
        {
            if (AdminText.Matches(c.FullName, q) || AdminText.Matches(c.Coach.Email, q))
            {
                Coaches.Add(c);
            }
        }
        OnPropertyChanged(nameof(IsEmpty));
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

    private static bool IsEmail(string s)
    {
        var at = s.IndexOf('@');
        return at > 0 && at < s.Length - 3 && s.IndexOf('.', at) > at + 1 && !s.Contains(' ');
    }
}
