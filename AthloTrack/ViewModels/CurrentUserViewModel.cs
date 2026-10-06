using System;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;
using AthloTrack.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AthloTrack.ViewModels;

/// <summary>
/// The signed-in user (coach or athlete): name and own photo. One shared instance, so the
/// drawer header, Ρυθμίσεις and the profile all show the same photo after it changes.
/// </summary>
public sealed partial class CurrentUserViewModel : ObservableObject
{
    private readonly SessionState _session;
    private readonly IAvatarService _avatars;
    private readonly ICoachRepository _coaches;
    private readonly IAthleteRepository _athletes;

    public CurrentUserViewModel(SessionState session, IAvatarService avatars, ICoachRepository coaches, IAthleteRepository athletes)
    {
        _session = session;
        _avatars = avatars;
        _coaches = coaches;
        _athletes = athletes;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Initial))]
    public partial string DisplayName { get; set; } = string.Empty;

    public string Initial => string.IsNullOrEmpty(DisplayName) ? "?" : DisplayName.Substring(0, 1).ToUpperInvariant();

    [ObservableProperty]
    public partial string RoleText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial byte[]? Photo { get; set; }

    [ObservableProperty]
    public partial bool IsUploading { get; set; }

    /// <summary>Coaches and athletes have a photo; admins don't (no storage folder of their own).</summary>
    public bool CanChangePhoto => !_session.IsAdmin;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>Refreshes from the session after sign-in.</summary>
    public async Task LoadAsync()
    {
        DisplayName = _session.DisplayName ?? string.Empty;
        RoleText = _session.IsAdmin ? "Διαχειριστής" : _session.IsCoach ? "Προπονητής" : "Αθλητής";
        OnPropertyChanged(nameof(CanChangePhoto));
        Photo = await _avatars.GetAsync(_session.ProfileImagePath);
    }

    /// <summary>Stores a new photo of the signed-in user (already resized by the view).</summary>
    public async Task UploadPhotoAsync(byte[] image, string contentType)
    {
        if (_session.ProfileId is not { } id) return;
        IsUploading = true;
        ErrorMessage = null;
        try
        {
            var oldPath = _session.ProfileImagePath;
            var newPath = await _avatars.UploadAsync(id, image, contentType);

            if (_session.IsCoach)
            {
                await _coaches.SetProfileImagePathAsync(id, newPath);
            }
            else if (await _athletes.GetByIdAsync(id) is { } athlete)
            {
                athlete.ProfileImagePath = newPath;
                await _athletes.UpdateAsync(athlete);
            }

            _session.ProfileImagePath = newPath;
            Photo = image;
            await _avatars.RemoveAsync(oldPath);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Η φωτογραφία δεν αποθηκεύτηκε: {ex.Message}";
        }
        finally
        {
            IsUploading = false;
        }
    }

    /// <summary>Called when the athlete changed their own photo or name from their profile.</summary>
    public void OnOwnAthleteChanged(Athlete athlete, byte[]? photo)
    {
        _session.ProfileImagePath = athlete.ProfileImagePath;
        _session.DisplayName = athlete.FullName;
        DisplayName = athlete.FullName;
        if (photo is not null) Photo = photo;
    }
}
