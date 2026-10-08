using AthloTrack.Core.Auth;

namespace AthloTrack.Services;

/// <summary>
/// Whether «Ολοκληρώθηκε» / «Δεν ολοκληρώθηκε» first ask «Σίγουρα; Αυτό δεν μπορεί να αναιρεθεί.»
/// (Ρυθμίσεις, per device; on unless turned off).
/// </summary>
public sealed class WorkoutAnswerSettings
{
    public const string Key = "workout.confirm_answer";

    private readonly IAppPreferences _preferences;

    public WorkoutAnswerSettings(IAppPreferences preferences)
    {
        _preferences = preferences;
    }

    public bool ConfirmAnswers
    {
        get => _preferences.Get(Key) != "0";
        set => _preferences.Set(Key, value ? null : "0");
    }

    public const string ConfirmText = "Σίγουρα; Αυτό δεν μπορεί να αναιρεθεί.";
}
