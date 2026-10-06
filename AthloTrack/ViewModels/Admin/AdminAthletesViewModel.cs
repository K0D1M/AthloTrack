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

public sealed class AdminAthleteItem
{
    public AdminAthleteItem(AdminAthlete athlete)
    {
        Athlete = athlete;
    }

    public AdminAthlete Athlete { get; }
    public string FullName => Athlete.FullName;
    public string Initial => string.IsNullOrEmpty(Athlete.FullName) ? "?" : Athlete.FullName[..1].ToUpperInvariant();
    public string CoachLine => $"Προπονητής: {Athlete.CoachName}";
    public string Email => string.IsNullOrWhiteSpace(Athlete.Email) ? "χωρίς email" : Athlete.Email!;
    public string Summary =>
        $"{Athlete.Workouts} ασκησιολόγια · {Athlete.Measurements} μετρήσεις · Σύνδεση: {AdminText.Ago(Athlete.LastSignInAt)}";
    public bool IsLinked => Athlete.IsLinked;
    public bool IsUnlinked => !Athlete.IsLinked;
    public bool HasApp => Athlete.HasApp;
}

/// <summary>A coach to pick in the filter or the move panel (null id: every coach).</summary>
public sealed record CoachChoice(Guid? Id, string Name)
{
    public override string ToString() => Name;
}

public enum LinkFilter
{
    All,
    Linked,
    Unlinked,
}

/// <summary>Αθλητές (admin): every athlete across coaches; move to another coach, delete.</summary>
public partial class AdminAthletesViewModel : ViewModelBase
{
    private static readonly CoachChoice AnyCoach = new(null, "Όλοι οι προπονητές");

    private readonly IAdminRepository _admin;
    private List<AdminAthleteItem> _all = new();
    private bool _hasLoaded;

    public AdminAthletesViewModel(IAdminRepository admin)
    {
        _admin = admin;
        _ = LoadAsync();
    }

    public ObservableCollection<AdminAthleteItem> Athletes { get; } = new();
    public ObservableCollection<CoachChoice> CoachFilterChoices { get; } = new() { AnyCoach };
    public ConfirmPrompt Confirm { get; } = new();

    [ObservableProperty]
    public partial string Search { get; set; } = string.Empty;

    [ObservableProperty]
    public partial CoachChoice? CoachFilter { get; set; } = AnyCoach;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAll), nameof(ShowLinked), nameof(ShowUnlinked))]
    public partial LinkFilter Link { get; set; }

    public bool ShowAll { get => Link == LinkFilter.All; set { if (value) Link = LinkFilter.All; } }
    public bool ShowLinked { get => Link == LinkFilter.Linked; set { if (value) Link = LinkFilter.Linked; } }
    public bool ShowUnlinked { get => Link == LinkFilter.Unlinked; set { if (value) Link = LinkFilter.Unlinked; } }

    partial void OnSearchChanged(string value) => ApplyFilter();
    partial void OnCoachFilterChanged(CoachChoice? value) => ApplyFilter();
    partial void OnLinkChanged(LinkFilter value) => ApplyFilter();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSkeleton), nameof(IsEmpty))]
    public partial bool IsLoading { get; set; }

    public bool ShowSkeleton => IsLoading && !_hasLoaded;
    public bool IsEmpty => !IsLoading && Athletes.Count == 0 && ErrorMessage is null;

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

    [RelayCommand]
    private void DismissIssued() => Issued = null;

    // ---- Move to another coach (panel) ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMoving), nameof(MoveTitle))]
    public partial AdminAthleteItem? Moving { get; set; }

    public bool IsMoving => Moving is not null;
    public string MoveTitle => Moving is null ? "" : $"Μεταφορά του/της {Moving.FullName} σε:";
    public ObservableCollection<CoachChoice> MoveTargets { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmMoveCommand))]
    public partial CoachChoice? MoveTarget { get; set; }

    [RelayCommand]
    private void Move(AdminAthleteItem? item)
    {
        if (item is null) return;
        MoveTargets.Clear();
        foreach (var c in CoachFilterChoices)
        {
            if (c.Id is not null && c.Id != item.Athlete.CoachId) MoveTargets.Add(c);
        }
        MoveTarget = null;
        Moving = item;
    }

    [RelayCommand]
    private void CancelMove() => Moving = null;

    private bool CanConfirmMove() => MoveTarget?.Id is not null;

    [RelayCommand(CanExecute = nameof(CanConfirmMove))]
    private async Task ConfirmMoveAsync()
    {
        if (Moving is not { } item || MoveTarget?.Id is not { } coachId) return;
        var to = MoveTarget.Name;
        Moving = null;
        await RunAsync(async () =>
        {
            await _admin.MoveAthleteAsync(item.Athlete.Id, coachId);
            StatusMessage = $"Ο/Η {item.FullName} ανήκει τώρα στον/στην {to}.";
            await LoadAsync();
        });
    }

    // ---- Password, delete ----

    [RelayCommand]
    private void ResetPassword(AdminAthleteItem? item)
    {
        if (item?.Athlete.AuthUserId is not { } userId) return;
        Confirm.Ask($"Νέος προσωρινός κωδικός για {item.FullName}; Ο τωρινός κωδικός παύει να ισχύει.",
            "Νέος κωδικός", () => RunAsync(async () =>
            {
                var result = await _admin.ResetPasswordAsync(userId);
                Issued = new IssuedPassword(item.FullName, item.Email, result.TempPassword ?? "");
                StatusMessage = null;
            }));
    }

    [RelayCommand]
    private void Delete(AdminAthleteItem? item)
    {
        if (item is null) return;
        Confirm.Ask($"Διαγραφή του/της {item.FullName}; Διαγράφονται οριστικά όλες οι μετρήσεις, τα ασκησιολόγια και οι ειδοποιήσεις. " +
                    (item.IsLinked ? "Ο λογαριασμός σύνδεσης μένει." : ""),
            "Διαγραφή", () => RunAsync(async () =>
            {
                await _admin.DeleteAthleteAsync(item.Athlete.Id);
                StatusMessage = $"Διαγράφηκε: {item.FullName}.";
                await LoadAsync();
            }), danger: true, requiredText: item.FullName);
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var athletes = await _admin.GetAthletesAsync();
            var coaches = await _admin.GetCoachesAsync();
            _all = athletes.Select(a => new AdminAthleteItem(a)).ToList();

            var selected = CoachFilter?.Id;
            CoachFilterChoices.Clear();
            CoachFilterChoices.Add(AnyCoach);
            foreach (var c in coaches) CoachFilterChoices.Add(new CoachChoice(c.Id, c.FullName));
            CoachFilter = CoachFilterChoices.FirstOrDefault(c => c.Id == selected) ?? AnyCoach;

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
        var coachId = CoachFilter?.Id;
        Athletes.Clear();
        foreach (var a in _all)
        {
            if (coachId is not null && a.Athlete.CoachId != coachId) continue;
            if (Link == LinkFilter.Linked && !a.IsLinked) continue;
            if (Link == LinkFilter.Unlinked && a.IsLinked) continue;
            if (!AdminText.Matches(a.FullName, q) && !AdminText.Matches(a.Athlete.Email, q)) continue;
            Athletes.Add(a);
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
}
