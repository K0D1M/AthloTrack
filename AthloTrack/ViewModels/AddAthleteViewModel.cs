using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;
using AthloTrack.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

public partial class AddAthleteViewModel : ObservableValidator
{
    private readonly IAthleteRepository _athletes;
    private readonly SessionState _session;
    private readonly CurrentUserViewModel? _currentUser;

    public AddAthleteViewModel(IAthleteRepository athletes, SessionState session, CurrentUserViewModel? currentUser = null)
    {
        _athletes = athletes;
        _session = session;
        _currentUser = currentUser;
    }

    /// <summary>Email and notes are the coach's to set; an athlete editing themselves doesn't see them.</summary>
    public bool ShowCoachFields => _session.IsCoach;

    public event Action? Saved;
    public event Action? Cancelled;

    private Athlete? _editing;

    /// <summary>Page title: add vs. edit.</summary>
    public string Title => _editing is null ? "Νέος αθλητής" : "Επεξεργασία αθλητή";

    /// <summary>Switches the form to editing an existing athlete, pre-filling its fields.</summary>
    public void BeginEdit(Athlete athlete)
    {
        _editing = athlete;
        FullName = athlete.FullName;
        DateOfBirth = athlete.DateOfBirth is { } dob
            ? new DateTimeOffset(dob.ToDateTime(TimeOnly.MinValue))
            : null;
        HeightCm = athlete.HeightCm?.ToString(System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty;
        Notes = athlete.Notes ?? string.Empty;
        Email = athlete.Email ?? string.Empty;
        OnPropertyChanged(nameof(Title));
    }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Το όνομα είναι υποχρεωτικό.")]
    [MinLength(2, ErrorMessage = "Πολύ σύντομο όνομα.")]
    public partial string FullName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateTimeOffset? DateOfBirth { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [RegularExpression(@"^$|^\d{1,3}([.,]\d)?$", ErrorMessage = "Μη έγκυρο ύψος (π.χ. 178).")]
    public partial string HeightCm { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Notes { get; set; } = string.Empty;

    /// <summary>The athlete's login email: signing up with it links the account to this athlete.</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [RegularExpression(@"^$|^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Μη έγκυρο email.")]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        ValidateAllProperties();
        if (HasErrors)
        {
            ErrorMessage = "Παρακαλώ διορθώστε τα πεδία.";
            return;
        }

        if (_session.ProfileId is not { } coachId)
        {
            ErrorMessage = "Δεν βρέθηκε ο προπονητής της συνεδρίας.";
            return;
        }

        IsBusy = true;
        try
        {
            decimal? height = null;
            if (!string.IsNullOrWhiteSpace(HeightCm) &&
                decimal.TryParse(HeightCm.Replace(',', '.'), System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var h))
            {
                height = h;
            }

            var dateOfBirth = DateOfBirth is { } d ? DateOnly.FromDateTime(d.DateTime) : (DateOnly?)null;
            var notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
            var email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim().ToLowerInvariant();

            if (_editing is { } existing)
            {
                // Keep id, coach, login link and photo; only the form's fields change.
                existing.FullName = FullName.Trim();
                existing.DateOfBirth = dateOfBirth;
                existing.HeightCm = height;
                if (_session.IsCoach)
                {
                    existing.Notes = notes;
                    existing.Email = email;
                }
                await _athletes.UpdateAsync(existing);

                if (!_session.IsCoach && existing.Id == _session.ProfileId)
                {
                    _currentUser?.OnOwnAthleteChanged(existing, null);
                }
            }
            else
            {
                await _athletes.AddAsync(new Athlete
                {
                    CoachId = coachId,
                    FullName = FullName.Trim(),
                    DateOfBirth = dateOfBirth,
                    HeightCm = height,
                    Notes = notes,
                    Email = email,
                });
            }

            Saved?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
