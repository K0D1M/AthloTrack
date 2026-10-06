using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AthloTrack.Core.Data;
using AthloTrack.Core.Models.Admin;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

/// <summary>A number on the overview: title, value, and a line under it.</summary>
public sealed record AdminKpi(string Title, string Value, string Detail);

/// <summary>One health check: how many rows need a look, and the rows themselves.</summary>
public sealed record AdminHealthCheck(string Title, string Description, IReadOnlyList<HealthItem> Items)
{
    public int Count => Items.Count;
    public bool HasIssues => Items.Count > 0;
    public string CountText => Items.Count.ToString();
}

public sealed record AdminActivityRow(string Kind, string Text, string Who, string When);

/// <summary>Επισκόπηση — the admin's home: numbers, health checks and the latest activity.</summary>
public partial class AdminOverviewViewModel : ViewModelBase
{
    private readonly IAdminRepository _admin;
    private bool _hasLoaded;

    public AdminOverviewViewModel(IAdminRepository admin)
    {
        _admin = admin;
        _ = LoadAsync();
    }

    public ObservableCollection<AdminKpi> Kpis { get; } = new();
    public ObservableCollection<AdminHealthCheck> Checks { get; } = new();
    public ObservableCollection<AdminActivityRow> Activity { get; } = new();

    /// <summary>The shell opens the check's list as a sub-page.</summary>
    public event Action<AdminHealthCheck>? OpenCheckRequested;

    /// <summary>The shell opens «Ιστορικό ενεργειών».</summary>
    public event Action? OpenAuditRequested;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSkeleton))]
    public partial bool IsLoading { get; set; }

    public bool ShowSkeleton => IsLoading && !_hasLoaded;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool AllHealthy { get; set; }

    [RelayCommand]
    private void OpenCheck(AdminHealthCheck? check)
    {
        if (check is { HasIssues: true }) OpenCheckRequested?.Invoke(check);
    }

    [RelayCommand]
    private void OpenAudit() => OpenAuditRequested?.Invoke();

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var overview = await _admin.GetOverviewAsync();
            var health = await _admin.GetHealthAsync();
            var activity = await _admin.GetActivityAsync(40);

            Kpis.Clear();
            foreach (var kpi in BuildKpis(overview)) Kpis.Add(kpi);

            Checks.Clear();
            foreach (var check in BuildChecks(health)) Checks.Add(check);
            AllHealthy = Checks.Count > 0 && !HasAnyIssue();

            Activity.Clear();
            foreach (var e in activity)
            {
                Activity.Add(new AdminActivityRow(AdminText.Kind(e.Kind), e.Text, e.Who, AdminText.Ago(e.At)));
            }
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

    private bool HasAnyIssue()
    {
        foreach (var c in Checks) if (c.HasIssues) return true;
        return false;
    }

    public static IEnumerable<AdminKpi> BuildKpis(AdminOverview o)
    {
        yield return new AdminKpi("Προπονητές", o.Coaches.ToString(), "");
        yield return new AdminKpi("Αθλητές", o.Athletes.ToString(),
            $"{o.LinkedAthletes} με λογαριασμό");
        yield return new AdminKpi("Ενεργοί χρήστες", o.Active7Days.ToString(),
            $"7 ημέρες · {o.Active30Days} σε 30 ημέρες");
        yield return new AdminKpi("Με την εφαρμογή", o.AppUsers.ToString(),
            $"από {o.Logins} λογαριασμούς");
        yield return new AdminKpi("Ασκησιολόγια", o.WorkoutsThisWeek.ToString(),
            $"αυτή την εβδομάδα · {o.WorkoutsThisMonth} τον μήνα · {o.WorkoutsTotal} συνολικά");
        yield return new AdminKpi("Ολοκλήρωση", AdminText.Percent(o.Completed30Days, o.Due30Days),
            $"{o.Completed30Days} από {o.Due30Days} (30 ημέρες)");
        yield return new AdminKpi("Διαβάστηκαν", AdminText.Percent(o.Read30Days, o.Due30Days),
            $"{o.Read30Days} από {o.Due30Days} (30 ημέρες)");
        yield return new AdminKpi("Μετρήσεις", o.MeasurementsThisMonth.ToString(), "αυτόν τον μήνα");
    }

    public static IEnumerable<AdminHealthCheck> BuildChecks(HealthReport h)
    {
        yield return new AdminHealthCheck("Λογαριασμοί χωρίς προφίλ",
            "Συνδέονται αλλά δεν βλέπουν τίποτα: συνήθως αθλητές που έκαναν εγγραφή πριν τους προσθέσει ο προπονητής, ή με άλλο email.",
            h.OrphanLogins);
        yield return new AdminHealthCheck("Αθλητές χωρίς email",
            "Δεν μπορούν να αποκτήσουν λογαριασμό μέχρι ο προπονητής να γράψει το email τους.",
            h.AthletesNoEmail);
        yield return new AdminHealthCheck("Αθλητές χωρίς λογαριασμό",
            "Έχουν email αλλά δεν έχουν κάνει ακόμα εγγραφή στην εφαρμογή.",
            h.AthletesUnlinked);
        yield return new AdminHealthCheck("Ανενεργοί αθλητές",
            "Καμία νέα μέτρηση ή ασκησιολόγιο τις τελευταίες 30 ημέρες.",
            h.AthletesInactive);
        yield return new AdminHealthCheck("Προπονητές χωρίς δικό τους κωδικό",
            "Δεν έχουν αλλάξει τον προσωρινό κωδικό εδώ και πάνω από 7 ημέρες.",
            h.CoachesPendingPassword);
        yield return new AdminHealthCheck("Συσκευές χωρίς προφίλ",
            "Συσκευές που λαμβάνουν ειδοποιήσεις για λογαριασμούς χωρίς προφίλ.",
            h.OrphanDevices);
    }
}

/// <summary>One health check's rows (sub-page of Επισκόπηση).</summary>
public partial class AdminHealthListViewModel : ViewModelBase
{
    public AdminHealthListViewModel(AdminHealthCheck check)
    {
        Check = check;
    }

    public AdminHealthCheck Check { get; }
    public string Description => Check.Description;
    public IReadOnlyList<HealthItem> Items => Check.Items;
}

public sealed record AdminAuditRow(string Action, string Target, string When, string Admin);

/// <summary>Ιστορικό ενεργειών — what admins did (sub-page of Επισκόπηση).</summary>
public partial class AdminAuditViewModel : ViewModelBase
{
    private readonly IAdminRepository _admin;
    private bool _hasLoaded;

    public AdminAuditViewModel(IAdminRepository admin)
    {
        _admin = admin;
        _ = LoadAsync();
    }

    public ObservableCollection<AdminAuditRow> Entries { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSkeleton), nameof(IsEmpty))]
    public partial bool IsLoading { get; set; }

    public bool ShowSkeleton => IsLoading && !_hasLoaded;
    public bool IsEmpty => !IsLoading && Entries.Count == 0 && ErrorMessage is null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial string? ErrorMessage { get; set; }

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var entries = await _admin.GetAuditLogAsync();
            Entries.Clear();
            foreach (var e in entries)
            {
                Entries.Add(new AdminAuditRow(AdminText.Action(e.Action), e.Target ?? "", AdminText.When(e.CreatedAt), e.AdminEmail));
            }
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
}
