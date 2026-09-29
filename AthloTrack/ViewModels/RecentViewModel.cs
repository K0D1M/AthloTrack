using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;
using AthloTrack.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

/// <summary>Πρόσφατα — welcome screen: recent athlete (coach) or unread notifications (athlete).</summary>
public partial class RecentViewModel : ViewModelBase
{
    private readonly SessionState _session;
    private readonly IAthleteRepository _athletes;
    private readonly INotificationRepository _notifications;
    private readonly IAvatarService _avatars;
    private readonly ICoachRepository _coaches;

    public RecentViewModel(SessionState session, IAthleteRepository athletes, INotificationRepository notifications, IAvatarService avatars, ICoachRepository coaches)
    {
        _avatars = avatars;
        _coaches = coaches;
        _session = session;
        _athletes = athletes;
        _notifications = notifications;
        WelcomeMessage = $"Χαίρε, {_session.DisplayName ?? string.Empty}".TrimEnd(',', ' ') + "!";
        _ = LoadAsync();
    }

    public ObservableCollection<AppNotification> Notifications { get; } = new();

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
    public partial bool IsLoading { get; set; }

    [RelayCommand]
    private async Task MarkReadAsync(AppNotification? notification)
    {
        if (notification is null) return;
        try
        {
            await _notifications.MarkReadAsync(notification.Id);
            Notifications.Remove(notification);
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

                if (_session.CoachId is { } coachId && await _coaches.GetByIdAsync(coachId) is { } coach)
                {
                    CoachName = coach.FullName;
                    CoachInitial = coach.FullName.Length > 0 ? coach.FullName.Substring(0, 1).ToUpperInvariant() : "?";
                    HasCoach = true;
                    CoachPhoto = await _avatars.GetAsync(coach.ProfileImagePath);
                }
                return;
            }

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
            RecentAthleteSummary = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
