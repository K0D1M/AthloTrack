using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;
using AthloTrack.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

/// <summary>Πρόσφατα — welcome screen: recent athlete (coach) or unread notifications (athlete).</summary>
public partial class RecentViewModel : ViewModelBase, IRefreshable
{
    /// <summary>Pull down / ⟳: reload in place (a load already running covers it).</summary>
    public Task RefreshAsync() => IsLoading ? Task.CompletedTask : LoadAsync();

    private readonly SessionState _session;
    private readonly IAthleteRepository _athletes;
    private readonly INotificationRepository _notifications;
    private readonly IAvatarService _avatars;
    private readonly ICoachRepository _coaches;
    private readonly IFormRepository? _forms;

    public RecentViewModel(SessionState session, IAthleteRepository athletes, INotificationRepository notifications, IAvatarService avatars, ICoachRepository coaches,
        PushPromptViewModel pushPrompt, IFormRepository? forms = null)
    {
        _forms = forms;
        Push = pushPrompt;
        _avatars = avatars;
        _coaches = coaches;
        _session = session;
        _athletes = athletes;
        _notifications = notifications;
        // Greek greets in the vocative: «Χαίρε, Γιώργο!» (not «Γιώργος»).
        WelcomeMessage = $"Χαίρε, {AthloTrack.Core.Text.GreekVocative.Of(_session.DisplayName)}".TrimEnd(',', ' ') + "!";
        _ = LoadAsync();
        if (session.Role == UserRole.Athlete) _ = LoadOpenFormsAsync();
        if (session.IsCoach) _ = LoadRecentFormsAsync();
    }

    public ObservableCollection<AppNotification> Notifications { get; } = new();

    /// <summary>The notifications as shown: the person's photo with a badge, their name in bold.</summary>
    public ObservableCollection<NotificationItem> NotificationItems { get; } = new();

    /// <summary>«5 νέες», «1 νέα», «Όλα ενημερωμένα».</summary>
    public string NotificationsSummary => NotificationItems.Count switch
    {
        0 => "Όλα ενημερωμένα",
        1 => "1 νέα",
        var n => $"{n} νέες",
    };

    public bool HasNotifications => NotificationItems.Count > 0;

    [RelayCommand]
    private Task OpenItemAsync(NotificationItem? item) => OpenNotificationAsync(item?.Notification);

    [RelayCommand]
    private Task ReadItemAsync(NotificationItem? item) => MarkReadAsync(item?.Notification);

    /// <summary>«Ανάγνωση όλων».</summary>
    [RelayCommand]
    private async Task MarkAllReadAsync()
    {
        foreach (var n in Notifications.ToList()) await MarkReadAsync(n);
    }

    /// <summary>Rebuilds the shown list; the photos follow, one download per person.</summary>
    private void ShowNotifications(Func<AppNotification, (string? Name, string? PhotoPath)> person)
    {
        NotificationItems.Clear();
        var byPath = new Dictionary<string, List<NotificationItem>>();
        foreach (var n in Notifications)
        {
            var (name, path) = person(n);
            var item = new NotificationItem(n, name);
            NotificationItems.Add(item);
            if (path is not null)
            {
                if (!byPath.TryGetValue(path, out var list)) byPath[path] = list = new();
                list.Add(item);
            }
        }
        OnNotificationsChanged();
        foreach (var (path, items) in byPath) _ = LoadNotificationPhotoAsync(path, items);
    }

    private async Task LoadNotificationPhotoAsync(string path, List<NotificationItem> items)
    {
        try
        {
            var photo = await _avatars.GetAsync(path);
            foreach (var i in items) i.Photo = photo;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AthloTrack] Notification photo: {ex.Message}");
        }
    }

    private void OnNotificationsChanged()
    {
        OnPropertyChanged(nameof(NotificationsSummary));
        OnPropertyChanged(nameof(HasNotifications));
    }

    /// <summary>Athlete: forms still to fill in («Έντυπο να συμπληρώσεις: …»).</summary>
    public ObservableCollection<OpenFormItem> OpenForms { get; } = new();

    /// <summary>An open form was tapped: the shell opens its fill-in page.</summary>
    public event Action<Guid>? OpenFormRequested;

    [RelayCommand]
    private void OpenForm(OpenFormItem? item)
    {
        if (item is not null) OpenFormRequested?.Invoke(item.AssignmentId);
    }

    /// <summary>Coach: the 3 latest submitted copies on the «Έντυπα» card.</summary>
    public ObservableCollection<CompletedFormItem> RecentForms { get; } = new();

    /// <summary>The coach has sent forms: the card shows.</summary>
    [ObservableProperty]
    public partial bool HasFormsCard { get; set; }

    /// <summary>«Υποβλήθηκαν πρόσφατα · 2 σε εκκρεμότητα».</summary>
    [ObservableProperty]
    public partial string FormsCardSummary { get; set; } = string.Empty;

    /// <summary>«Όλα τα έντυπα ›»: Έντυπα, on «Συμπληρωμένα».</summary>
    public event Action? OpenFormsRequested;

    [RelayCommand]
    private void ShowAllForms() => OpenFormsRequested?.Invoke();

    [RelayCommand]
    private void OpenRecentForm(CompletedFormItem? item)
    {
        if (item is not null) OpenFormRequested?.Invoke(item.CopyId);
    }

    private async Task LoadRecentFormsAsync()
    {
        if (_forms is null) return;
        try
        {
            var copies = await _forms.GetAllAssignmentsAsync();
            if (copies.Count == 0) return;
            var titles = (await _forms.GetFormsAsync()).ToDictionary(f => f.Id, f => f.Title);
            var names = (await _athletes.GetAllAsync()).ToDictionary(a => a.Id, a => a.FullName);
            RecentForms.Clear();
            foreach (var c in copies.Where(c => c.SubmittedAt is not null && titles.ContainsKey(c.FormId))
                         .OrderByDescending(c => c.SubmittedAt).Take(3))
                RecentForms.Add(new CompletedFormItem(c.Id, names.GetValueOrDefault(c.AthleteId) ?? "—", titles[c.FormId],
                    c.SubmittedAtLocal!.Value));
            var pending = copies.Count(c => !c.IsSubmitted);
            var latest = RecentForms.Count > 0 ? "Υποβλήθηκαν πρόσφατα" : "Κανένα δεν έχει υποβληθεί ακόμη";
            FormsCardSummary = pending == 0 ? latest : pending == 1 ? $"{latest} · 1 σε εκκρεμότητα" : $"{latest} · {pending} σε εκκρεμότητα";
            HasFormsCard = true;
        }
        catch (Exception ex)
        {
            // 018 not run yet, or offline: no card.
            Console.WriteLine($"[AthloTrack] Recent forms: {ex.Message}");
        }
    }

    private async Task LoadOpenFormsAsync()
    {
        if (_forms is null || _session.ProfileId is not { } athleteId) return;
        try
        {
            var copies = await _forms.GetAssignmentsForAthleteAsync(athleteId);
            var forms = await _forms.GetFormsAsync(); // only the ones sent to this athlete
            OpenForms.Clear();
            foreach (var c in copies)
            {
                if (c.IsSubmitted) continue;
                var form = System.Linq.Enumerable.FirstOrDefault(forms, f => f.Id == c.FormId);
                if (form is not null) OpenForms.Add(new OpenFormItem(c.Id, form.Title));
            }
        }
        catch (Exception ex)
        {
            // 018 not run yet, or offline: no card.
            Console.WriteLine($"[AthloTrack] Open forms: {ex.Message}");
        }
    }

    /// <summary>A notification card was tapped: the shell opens the workout it's about.</summary>
    public event Action<AppNotification>? OpenNotificationRequested;

    [RelayCommand]
    private async Task OpenNotificationAsync(AppNotification? notification)
    {
        if (notification is null) return;
        await MarkReadAsync(notification); // seen: it leaves the list
        OpenNotificationRequested?.Invoke(notification);
    }

    /// <summary>Web: a banner offering to turn on notifications, until they're on.</summary>
    public PushPromptViewModel Push { get; }

    public bool IsCoach => _session.IsCoach;
    public bool IsAthlete => _session.Role == UserRole.Athlete;

    [ObservableProperty]
    public partial string WelcomeMessage { get; set; } = "Χαίρε!";

    [ObservableProperty]
    public partial string? RecentAthleteName { get; set; }

    [ObservableProperty]
    public partial string? RecentAthleteSummary { get; set; }

    /// <summary>Photo of the most recent athlete; null shows the initial.</summary>
    [ObservableProperty]
    public partial byte[]? RecentAthletePhoto { get; set; }

    [ObservableProperty]
    public partial string? RecentAthleteInitial { get; set; }

    [ObservableProperty]
    public partial bool HasRecentAthlete { get; set; }

    // Athlete home: their coach
    [ObservableProperty]
    public partial string? CoachName { get; set; }

    [ObservableProperty]
    public partial string? CoachInitial { get; set; }

    [ObservableProperty]
    public partial byte[]? CoachPhoto { get; set; }

    [ObservableProperty]
    public partial bool HasCoach { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSkeleton))]
    public partial bool IsLoading { get; set; }

    /// <summary>
    /// The first load is running: show placeholders shaped like the content. Later reloads keep
    /// the content and only show the loading line.
    /// </summary>
    public bool ShowSkeleton => IsLoading && !_hasLoaded;

    private bool _hasLoaded;

    [RelayCommand]
    private async Task MarkReadAsync(AppNotification? notification)
    {
        if (notification is null) return;
        try
        {
            await _notifications.MarkReadAsync(notification.Id);
            Notifications.Remove(notification);
            foreach (var item in NotificationItems.Where(i => i.Notification == notification).ToList())
                NotificationItems.Remove(item);
            OnNotificationsChanged();
        }
        catch
        {
            // ignore — will reappear on next refresh
        }
    }

    private async Task LoadRecentPhotoAsync(string? path)
    {
        RecentAthletePhoto = await _avatars.GetAsync(path);
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            if (_session.Role == UserRole.Athlete && _session.ProfileId is { } athleteId)
            {
                Notifications.Clear();
                foreach (var n in await _notifications.GetUnreadForAthleteAsync(athleteId))
                    Notifications.Add(n);

                string? coachName = null, coachPhoto = null;
                if (_session.CoachId is { } coachId && await _coaches.GetByIdAsync(coachId) is { } coach)
                {
                    CoachName = coach.FullName;
                    CoachInitial = coach.FullName.Length > 0 ? coach.FullName.Substring(0, 1).ToUpperInvariant() : "?";
                    HasCoach = true;
                    coachName = coach.FullName;
                    coachPhoto = coach.ProfileImagePath;
                }
                // An athlete's notifications come from their coach.
                ShowNotifications(_ => (coachName, coachPhoto));
                if (coachPhoto is not null) CoachPhoto = await _avatars.GetAsync(coachPhoto);
                return;
            }

            // Coach: e.g. "Ο/Η ... ολοκλήρωσε το ασκησιολόγιο".
            Notifications.Clear();
            foreach (var n in await _notifications.GetUnreadForCoachAsync())
                Notifications.Add(n);
            // A coach's notifications are about an athlete: their name and photo.
            var athletes = (await _athletes.GetAllAsync()).ToDictionary(a => a.Id);
            ShowNotifications(n => athletes.TryGetValue(n.AthleteId, out var a) ? (a.FullName, a.ProfileImagePath) : (null, null));

            var athlete = await _athletes.GetMostRecentAsync();
            if (athlete is not null)
            {
                RecentAthleteName = athlete.FullName;
                HasRecentAthlete = true;
                RecentAthleteInitial = athlete.FullName.Length > 0 ? athlete.FullName.Substring(0, 1).ToUpperInvariant() : "?";
                _ = LoadRecentPhotoAsync(athlete.ProfileImagePath);
                var parts = new System.Collections.Generic.List<string>();
                if (athlete.HeightCm is { } h) parts.Add($"Ύψος: {h} cm");
                if (athlete.DateOfBirth is { } dob) parts.Add($"Γεν.: {dob:dd/MM/yyyy}");
                parts.Add($"Ενημερώθηκε: {athlete.UpdatedAt.LocalDateTime:dd/MM/yyyy}");
                RecentAthleteSummary = string.Join("  •  ", parts);
            }
            else
            {
                RecentAthleteName = "Δεν υπάρχουν αθλητές ακόμη.";
            }
        }
        catch (Exception ex)
        {
            // The athlete home has no visible error slot; keep the cause in the console.
            Console.WriteLine($"[AthloTrack] Πρόσφατα failed to load: {ex.Message}");
            RecentAthleteSummary = ex.Message;
        }
        finally
        {
            _hasLoaded = true;
            IsLoading = false;
        }
    }
}
