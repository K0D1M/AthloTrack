using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;
using AthloTrack.Core.Models;
using AthloTrack.Core.Workouts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

/// <summary>An earlier workout or a saved template the coach can start from.</summary>
public sealed record ReuseItem(string Title, string Subtitle, string Content, Guid? TemplateId = null);

public partial class AddWorkoutViewModel : ObservableValidator
{
    private const char Separator = '\u001F';

    private readonly Guid _athleteId;
    private readonly IWorkoutRepository _workouts;
    private readonly SessionState _session;
    private readonly IAppPreferences _preferences;
    private readonly IWorkoutTemplateRepository? _templates;
    private readonly IAthleteRepository? _athletes;

    // Draft autosave: the key, and the values the form started from (empty for a new workout,
    // the stored workout for an edit). A draft equal to them isn't kept.
    private string _draftKey;
    private string _baseContent = string.Empty;
    private DateOnly _baseDate = DateOnly.FromDateTime(DateTime.Today);
    private bool? _basePresent;
    private bool _suspendDraft;

    private Func<Task>? _pendingConfirm;
    private bool _reuseLoaded;

    public AddWorkoutViewModel(Guid athleteId, string athleteName, IWorkoutRepository workouts, SessionState session,
        IAppPreferences? preferences = null, IWorkoutTemplateRepository? templates = null, IAthleteRepository? athletes = null)
    {
        _athleteId = athleteId;
        _workouts = workouts;
        _session = session;
        _preferences = preferences ?? new InMemoryAppPreferences();
        _templates = templates;
        _athletes = athletes;
        Title = $"Νέο ασκησιολόγιο για τον αθλητή {athleteName}";
        if (!string.IsNullOrEmpty(athleteName)) Initial = athleteName.Substring(0, 1).ToUpperInvariant();

        _draftKey = $"workout.draft.new.{athleteId}";
        RestoreDraft();
    }

    public event Action? Saved;
    public event Action? Cancelled;

    /// <summary>
    /// The builder's line, for the view to insert at the caret. Without a listener (tests) the
    /// line is appended to the text.
    /// </summary>
    public event Action<string>? InsertRequested;

    /// <summary>Set by <see cref="BeginEdit"/>: the workout being changed instead of a new one.</summary>
    private Guid? _editingId;

    /// <summary>Header shown at the top of the form.</summary>
    public string Title { get; private set; }

    /// <summary>Top-bar title of the page.</summary>
    public string PageTitle => _editingId is null ? "Νέο ασκησιολόγιο" : "Επεξεργασία ασκησιολογίου";

    /// <summary>Pre-fills the form with an existing workout (coach edit).</summary>
    public void BeginEdit(WorkoutProgram workout, string athleteName)
    {
        _editingId = workout.Id;
        _draftKey = $"workout.draft.edit.{workout.Id}";
        _baseContent = workout.Content;
        _baseDate = workout.TargetDate;
        _basePresent = workout.CoachPresent;
        // The constructor may have restored this athlete's new-workout draft: an edit doesn't use it.
        IsDraftRestored = false;
        ApplyBase();
        Title = $"Επεξεργασία ασκησιολογίου για τον αθλητή {athleteName}";
        RestoreDraft();
    }

    /// <summary>The athlete's photo for the header; null shows <see cref="Initial"/>.</summary>
    [ObservableProperty]
    public partial byte[]? Photo { get; set; }

    public string Initial { get; private set; } = "?";

    [ObservableProperty]
    public partial DateTimeOffset TargetDate { get; set; } = DateTimeOffset.Now;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Το ασκησιολόγιο δεν μπορεί να είναι κενό.")]
    [MinLength(3, ErrorMessage = "Πολύ σύντομο κείμενο.")]
    public partial string Content { get; set; } = string.Empty;

    /// <summary>Optional: true Παρών, false Απών, null not said (no indicator for the athlete).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPresentChosen), nameof(IsAbsentChosen))]
    public partial bool? CoachPresent { get; set; }

    /// <summary>The «Παρών» toggle. Tapping it again when chosen clears the choice.</summary>
    public bool IsPresentChosen
    {
        get => CoachPresent == true;
        set => CoachPresent = value ? true : CoachPresent == true ? null : CoachPresent;
    }

    /// <summary>The «Απών» toggle. Tapping it again when chosen clears the choice.</summary>
    public bool IsAbsentChosen
    {
        get => CoachPresent == false;
        set => CoachPresent = value ? false : CoachPresent == false ? null : CoachPresent;
    }

    /// <summary>Shows the formatted text instead of the editor.</summary>
    [ObservableProperty]
    public partial bool IsPreview { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>Non-error feedback (e.g. "template saved").</summary>
    [ObservableProperty]
    public partial string? InfoMessage { get; set; }

    // ---- Draft autosave ----

    /// <summary>The form reopened with unsaved changes from before («Επαναφέρθηκε πρόχειρο»).</summary>
    [ObservableProperty]
    public partial bool IsDraftRestored { get; set; }

    partial void OnContentChanged(string value) => SaveDraft();
    partial void OnTargetDateChanged(DateTimeOffset value) => SaveDraft();
    partial void OnCoachPresentChanged(bool? value) => SaveDraft();

    /// <summary>Throws the restored draft away and goes back to how the form started.</summary>
    [RelayCommand]
    private void DiscardDraft()
    {
        _preferences.Set(_draftKey, null);
        ApplyBase();
        IsDraftRestored = false;
        ClearErrors();
    }

    private void SaveDraft()
    {
        if (_suspendDraft) return;
        var date = DateOnly.FromDateTime(TargetDate.DateTime);
        if (Content == _baseContent && date == _baseDate && CoachPresent == _basePresent)
        {
            _preferences.Set(_draftKey, null);
            return;
        }

        _preferences.Set(_draftKey, string.Join(Separator,
            "1", BaseStamp(), date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), PresentCode(CoachPresent), Content));
    }

    private void RestoreDraft()
    {
        var stored = _preferences.Get(_draftKey);
        if (stored is null) return;

        var parts = stored.Split(Separator, 5);
        // An edit draft only applies to the version it was started from: if the workout was changed
        // since (e.g. on another device), the draft would overwrite newer text, so it's dropped.
        if (parts.Length != 5 || parts[0] != "1" || parts[1] != BaseStamp()
            || !DateOnly.TryParseExact(parts[2], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            _preferences.Set(_draftKey, null);
            return;
        }

        _suspendDraft = true;
        Content = parts[4];
        TargetDate = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue));
        CoachPresent = parts[3] switch { "1" => true, "0" => false, _ => null };
        _suspendDraft = false;
        IsDraftRestored = true;
    }

    private void ApplyBase()
    {
        _suspendDraft = true;
        Content = _baseContent;
        TargetDate = new DateTimeOffset(_baseDate.ToDateTime(TimeOnly.MinValue));
        CoachPresent = _basePresent;
        _suspendDraft = false;
    }

    private string BaseStamp() =>
        _editingId is null ? string.Empty : Fingerprint($"{_baseContent}{Separator}{_baseDate:yyyy-MM-dd}{Separator}{PresentCode(_basePresent)}");

    private static string PresentCode(bool? present) => present switch { true => "1", false => "0", null => "" };

    /// <summary>FNV-1a: stable across runs, unlike string.GetHashCode.</summary>
    private static string Fingerprint(string value)
    {
        var hash = 14695981039346656037UL;
        foreach (var c in value)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }

        return hash.ToString("x16", CultureInfo.InvariantCulture);
    }

    // ---- Exercise builder ----

    [ObservableProperty]
    public partial bool IsBuilderOpen { get; set; }

    [ObservableProperty]
    public partial string ExerciseName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ExerciseSets { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ExerciseReps { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ExerciseKg { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ExerciseMinutes { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ExerciseDistance { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ExerciseRest { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? BuilderError { get; set; }

    [RelayCommand]
    private void ToggleBuilder()
    {
        IsBuilderOpen = !IsBuilderOpen;
        if (IsBuilderOpen) IsReuseOpen = false;
        BuilderError = null;
    }

    [RelayCommand]
    private void AddExercise()
    {
        BuilderError = null;
        if (string.IsNullOrWhiteSpace(ExerciseName))
        {
            BuilderError = "Γράψε το όνομα της άσκησης.";
            return;
        }

        if (!TryInt(ExerciseSets, "Σετ", out var sets) || !TryInt(ExerciseReps, "Επαναλήψεις", out var reps)
            || !TryDecimal(ExerciseKg, "Κιλά", out var kg) || !TryInt(ExerciseMinutes, "Λεπτά", out var minutes)
            || !TryInt(ExerciseRest, "Διάλειμμα", out var rest))
        {
            return;
        }

        var line = WorkoutMarkup.FormatExercise(ExerciseName, sets, reps, kg, minutes, ExerciseDistance, rest);
        if (InsertRequested is { } insert) insert(line);
        else Content = string.IsNullOrWhiteSpace(Content) ? line : Content.TrimEnd() + "\n" + line;

        // Ready for the next exercise: usually only the name changes.
        ExerciseName = string.Empty;
    }

    private bool TryInt(string text, string field, out int? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var v) && v > 0)
        {
            value = v;
            return true;
        }

        BuilderError = $"Μη έγκυρος αριθμός στο «{field}».";
        return false;
    }

    private bool TryDecimal(string text, string field, out decimal? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (decimal.TryParse(text.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v) && v > 0)
        {
            value = v;
            return true;
        }

        BuilderError = $"Μη έγκυρος αριθμός στο «{field}».";
        return false;
    }

    // ---- Reuse: earlier workouts and templates ----

    [ObservableProperty]
    public partial bool IsReuseOpen { get; set; }

    [ObservableProperty]
    public partial bool IsReuseLoading { get; set; }

    [ObservableProperty]
    public partial string? ReuseError { get; set; }

    public ObservableCollection<ReuseItem> Templates { get; } = new();
    public ObservableCollection<ReuseItem> Previous { get; } = new();

    [ObservableProperty]
    public partial bool HasNoTemplates { get; set; }

    [ObservableProperty]
    public partial bool HasNoPrevious { get; set; }

    [ObservableProperty]
    public partial string TemplateName { get; set; } = string.Empty;

    [RelayCommand]
    private async Task ToggleReuseAsync()
    {
        IsReuseOpen = !IsReuseOpen;
        if (!IsReuseOpen) return;
        IsBuilderOpen = false;
        if (!_reuseLoaded) await LoadReuseAsync();
    }

    public async Task LoadReuseAsync()
    {
        IsReuseLoading = true;
        ReuseError = null;
        try
        {
            Templates.Clear();
            if (_templates is not null)
            {
                foreach (var t in await _templates.GetAllAsync())
                    Templates.Add(new ReuseItem(t.Name, FirstLine(t.Content), t.Content, t.Id));
            }

            Previous.Clear();
            var names = _athletes is null
                ? new Dictionary<Guid, string>()
                : (await _athletes.GetAllAsync()).ToDictionary(a => a.Id, a => a.FullName);
            var earlier = (await _workouts.GetAllAsync())
                .Where(w => w.Id != _editingId && !string.IsNullOrWhiteSpace(w.Content))
                .OrderByDescending(w => w.CreatedAt).ThenByDescending(w => w.TargetDate)
                .DistinctBy(w => w.Content.Trim())
                .Take(30);
            foreach (var w in earlier)
            {
                var name = names.TryGetValue(w.AthleteId, out var n) ? n : "—";
                Previous.Add(new ReuseItem(FirstLine(w.Content), $"{name} · {w.TargetDate:dd/MM/yyyy}", w.Content));
            }

            _reuseLoaded = true;
        }
        catch (Exception ex)
        {
            ReuseError = ex.Message;
        }
        finally
        {
            HasNoTemplates = Templates.Count == 0;
            HasNoPrevious = Previous.Count == 0;
            IsReuseLoading = false;
        }
    }

    /// <summary>Fills the editor with a template or earlier workout (asks first if there's text).</summary>
    [RelayCommand]
    private void UseItem(ReuseItem? item)
    {
        if (item is null) return;
        if (string.IsNullOrWhiteSpace(Content) || Content.Trim() == item.Content.Trim())
        {
            Apply();
            return;
        }

        Ask("Να αντικατασταθεί το κείμενο του ασκησιολογίου;", "Αντικατάσταση", () =>
        {
            Apply();
            return Task.CompletedTask;
        });

        void Apply()
        {
            Content = item.Content;
            IsReuseOpen = false;
            IsPreview = false;
        }
    }

    [RelayCommand]
    private async Task SaveTemplateAsync()
    {
        InfoMessage = null;
        ReuseError = null;
        if (_templates is null) return;
        if (string.IsNullOrWhiteSpace(TemplateName))
        {
            ReuseError = "Γράψε ένα όνομα για το πρότυπο.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Content))
        {
            ReuseError = "Το ασκησιολόγιο είναι κενό.";
            return;
        }

        if (_session.ProfileId is not { } coachId) return;
        try
        {
            var saved = await _templates.AddAsync(coachId, TemplateName.Trim(), Content.Trim());
            Templates.Insert(0, new ReuseItem(saved.Name, FirstLine(saved.Content), saved.Content, saved.Id));
            HasNoTemplates = false;
            InfoMessage = $"Αποθηκεύτηκε το πρότυπο «{saved.Name}».";
            TemplateName = string.Empty;
        }
        catch (Exception ex)
        {
            ReuseError = ex.Message;
        }
    }

    [RelayCommand]
    private void DeleteTemplate(ReuseItem? item)
    {
        if (item?.TemplateId is not { } id || _templates is null) return;
        Ask($"Διαγραφή του προτύπου «{item.Title}»;", "Διαγραφή", async () =>
        {
            await _templates.DeleteAsync(id);
            Templates.Remove(item);
            HasNoTemplates = Templates.Count == 0;
        });
    }

    private static string FirstLine(string content) =>
        WorkoutMarkup.PlainPreview(content).Split('\n')[0].Trim();

    // ---- In-app confirmation (the browser/Android heads have no native message box) ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConfirming))]
    public partial string? ConfirmMessage { get; set; }

    [ObservableProperty]
    public partial string ConfirmButtonText { get; set; } = "OK";

    public bool IsConfirming => ConfirmMessage is not null;

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        var action = _pendingConfirm;
        _pendingConfirm = null;
        ConfirmMessage = null;
        if (action is null) return;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ReuseError = ex.Message;
        }
    }

    [RelayCommand]
    private void CancelConfirm()
    {
        _pendingConfirm = null;
        ConfirmMessage = null;
    }

    private void Ask(string message, string button, Func<Task> onConfirm)
    {
        _pendingConfirm = onConfirm;
        ConfirmButtonText = button;
        ConfirmMessage = message;
    }

    // ---- Save / cancel ----

    [RelayCommand]
    private void Cancel()
    {
        _preferences.Set(_draftKey, null);
        Cancelled?.Invoke();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        ValidateAllProperties();
        if (HasErrors)
        {
            ErrorMessage = "Παρακαλώ συμπληρώστε το ασκησιολόγιο.";
            return;
        }

        IsBusy = true;
        try
        {
            if (_editingId is { } id)
            {
                // The DB clears the read receipt and sends the athlete "ενημέρωσε το ασκησιολόγιο".
                await _workouts.UpdateAsync(id, Content.Trim(), DateOnly.FromDateTime(TargetDate.DateTime), CoachPresent);
            }
            else
            {
                var program = new WorkoutProgram
                {
                    AthleteId = _athleteId,
                    Content = Content.Trim(),
                    TargetDate = DateOnly.FromDateTime(TargetDate.DateTime),
                    CreatedBy = _session.ProfileId,
                    CoachPresent = CoachPresent,
                };

                // The DB trigger auto-creates the athlete's "νέο ασκησιολόγιο" notification.
                await _workouts.AddAsync(program);
            }

            _preferences.Set(_draftKey, null);
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
