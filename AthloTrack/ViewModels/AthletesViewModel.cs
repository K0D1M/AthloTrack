using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

/// <summary>Αθλητές — list of athletes.</summary>
public partial class AthletesViewModel : ViewModelBase
{
    private readonly IAthleteRepository _athletes;
    private readonly SessionState _session;
    private readonly IAvatarService _avatars;

    public AthletesViewModel(IAthleteRepository athletes, SessionState session, IAvatarService avatars)
    {
        _athletes = athletes;
        _session = session;
        _avatars = avatars;
        _ = LoadAsync();
    }

    public ObservableCollection<AthleteListItemViewModel> Athletes { get; } = new();

    /// <summary>Raised when an athlete is chosen, so the shell can open their profile.</summary>
    public event Action<Guid>? OpenAthleteRequested;

    /// <summary>Raised when the coach taps "+" to add a new athlete.</summary>
    public event Action? AddAthleteRequested;

    /// <summary>True for coaches — controls whether the "add athlete" affordance shows.</summary>
    public bool CanEdit => _session.IsCoach;

    [RelayCommand]
    private void AddAthlete() => AddAthleteRequested?.Invoke();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSkeleton))]
    public partial bool IsLoading { get; set; }

    /// <summary>
    /// The first load is running: show placeholders shaped like the content. Later reloads keep
    /// the content and only show the loading line.
    /// </summary>
    public bool ShowSkeleton => IsLoading && !_hasLoaded;

    private bool _hasLoaded;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [RelayCommand]
    private void OpenAthlete(AthleteListItemViewModel? item)
    {
        if (item is not null)
        {
            OpenAthleteRequested?.Invoke(item.Id);
        }
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var list = await _athletes.GetAllAsync();
            Athletes.Clear();
            foreach (var a in list)
            {
                Athletes.Add(new AthleteListItemViewModel(a));
            }

            // Photos fill in after the names are on screen.
            foreach (var item in Athletes)
            {
                if (item.ProfileImagePath is not null)
                {
                    item.Photo = await _avatars.GetAsync(item.ProfileImagePath);
                }
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
