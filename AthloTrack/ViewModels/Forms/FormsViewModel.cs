using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;
using AthloTrack.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

/// <summary>
/// «Έντυπα» (coach), in two categories: «Όλα», the forms with how many athletes submitted each,
/// and «Συμπληρωμένα», every athlete's submitted copy, newest first, by day. The choice is remembered.
/// </summary>
public partial class FormsViewModel : ViewModelBase, IRefreshable
{
    /// <summary>Pull down / ⟳: reload in place (a load already running covers it).</summary>
    public Task RefreshAsync() => IsLoading ? Task.CompletedTask : LoadAsync();

    internal const string CategoryKey = "forms.category";
    private static readonly string[] Days = ["Κυριακή", "Δευτέρα", "Τρίτη", "Τετάρτη", "Πέμπτη", "Παρασκευή", "Σάββατο"];

    private readonly IFormRepository _forms;
    private readonly IAthleteRepository? _athletes;
    private readonly IAppPreferences? _preferences;

    public FormsViewModel(IFormRepository forms, IAthleteRepository? athletes = null, IAppPreferences? preferences = null,
        bool? showCompleted = null)
    {
        _forms = forms;
        _athletes = athletes;
        _preferences = preferences;
        ShowCompleted = showCompleted ?? _preferences?.Get(CategoryKey) == "completed";
        _ = LoadAsync();
    }

    public event Action? NewFormRequested;
    public event Action<FormDefinition>? OpenFormRequested;

    /// <summary>A submitted copy in «Συμπληρωμένα» was tapped: its answers open.</summary>
    public event Action<Guid>? OpenCopyRequested;

    public ObservableCollection<FormListItem> Forms { get; } = new();

    /// <summary>«Συμπληρωμένα»: the submitted copies under day headings.</summary>
    public ObservableCollection<CompletedFormDay> Completed { get; } = new();

    /// <summary>«Συμπληρωμένα» is shown (otherwise «Όλα»).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAll), nameof(IsEmpty), nameof(IsCompletedEmpty))]
    public partial bool ShowCompleted { get; set; }

    partial void OnShowCompletedChanged(bool value) => _preferences?.Set(CategoryKey, value ? "completed" : "all");

    public bool ShowAll => !ShowCompleted;

    [ObservableProperty]
    public partial int CompletedCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(IsCompletedEmpty))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public bool IsEmpty => ShowAll && !IsLoading && Forms.Count == 0 && ErrorMessage is null;
    public bool IsCompletedEmpty => ShowCompleted && !IsLoading && CompletedCount == 0 && ErrorMessage is null;

    [RelayCommand]
    private void ShowAllForms() => ShowCompleted = false;

    [RelayCommand]
    private void ShowCompletedForms() => ShowCompleted = true;

    [RelayCommand]
    private void NewForm() => NewFormRequested?.Invoke();

    [RelayCommand]
    private void OpenForm(FormListItem? item)
    {
        if (item is not null) OpenFormRequested?.Invoke(item.Form);
    }

    [RelayCommand]
    private void OpenCopy(CompletedFormItem? item)
    {
        if (item is not null) OpenCopyRequested?.Invoke(item.CopyId);
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var forms = await _forms.GetFormsAsync();
            var copies = await _forms.GetAllAssignmentsAsync();
            var names = _athletes is null ? new Dictionary<Guid, string>()
                : (await _athletes.GetAllAsync()).ToDictionary(a => a.Id, a => a.FullName);

            Forms.Clear();
            foreach (var f in forms)
            {
                var mine = copies.Where(a => a.FormId == f.Id).ToList();
                Forms.Add(new FormListItem(f, mine.Count, mine.Count(a => a.IsSubmitted)));
            }

            var titles = forms.ToDictionary(f => f.Id, f => f.Title);
            var byId = forms.ToDictionary(f => f.Id);
            var submitted = copies.Where(c => c.SubmittedAtLocal is not null && titles.ContainsKey(c.FormId))
                .OrderByDescending(c => c.SubmittedAt).ToList();
            CompletedCount = submitted.Count;
            Completed.Clear();
            var today = DateOnly.FromDateTime(DateTime.Today);
            foreach (var group in submitted.GroupBy(c => DateOnly.FromDateTime(c.SubmittedAtLocal!.Value.DateTime)))
            {
                var items = group.Select(c => new CompletedFormItem(c.Id, names.GetValueOrDefault(c.AthleteId) ?? "—",
                    titles[c.FormId], c.SubmittedAtLocal!.Value) { Form = byId[c.FormId], Copy = c }).ToList();
                var (title, sub) = DayTitles(group.Key, today);
                Completed.Add(new CompletedFormDay(title, sub, items));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(IsCompletedEmpty));
        }
    }

    /// <summary>«Σήμερα · Τρίτη 10/11», «Χθες · Δευτέρα 9/11», «Παρασκευή · 6/11».</summary>
    internal static (string Title, string Sub) DayTitles(DateOnly day, DateOnly today)
    {
        var date = day.Year == today.Year
            ? day.ToString("d/M", CultureInfo.InvariantCulture)
            : day.ToString("d/M/yyyy", CultureInfo.InvariantCulture);
        return (day.DayNumber - today.DayNumber) switch
        {
            0 => ("Σήμερα", $"{Days[(int)day.DayOfWeek]} {date}"),
            -1 => ("Χθες", $"{Days[(int)day.DayOfWeek]} {date}"),
            _ => (Days[(int)day.DayOfWeek], date),
        };
    }
}

/// <summary>A form in the list: «Υποβλήθηκαν 3 από 5».</summary>
public sealed class FormListItem(FormDefinition form, int sent, int submitted)
{
    public FormDefinition Form { get; } = form;
    public string Title => Form.Title;
    public int Sent { get; } = sent;
    public int Submitted { get; } = submitted;
    public bool HasPending => Submitted < Sent;

    /// <summary>Sent, and every athlete submitted it: the green pill.</summary>
    public bool IsAllDone => Sent > 0 && Submitted == Sent;

    public string Summary => Sent == 0 ? "Δεν έχει σταλεί ακόμη" : $"Υποβλήθηκαν {Submitted} από {Sent}";

    public string QuestionsText
    {
        get
        {
            var questions = Form.Questions.Count == 1 ? "1 ερώτηση" : $"{Form.Questions.Count} ερωτήσεις";
            var fields = AthloTrack.Core.Forms.FormFields.Parse(Form.Intro).Count;
            if (fields > 0)
                return (fields == 1 ? "1 πεδίο" : $"{fields} πεδία") + (Form.Questions.Count > 0 ? $" · {questions}" : "");
            return Form.IntroEditable ? $"Κείμενο για συμπλήρωση · {questions}" : questions;
        }
    }
}

/// <summary>A day heading in «Συμπληρωμένα».</summary>
public sealed record CompletedFormDay(string Title, string Subtitle, IReadOnlyList<CompletedFormItem> Items);

/// <summary>An athlete's submitted copy: «Μαρία · Ιστορικό υγείας · ✓ 18:42».</summary>
public sealed record CompletedFormItem(Guid CopyId, string AthleteName, string FormTitle, DateTimeOffset SubmittedAt)
{
    public string Initial => AthleteName.Length > 0 ? AthleteName[..1].ToUpperInvariant() : "?";
    public string TimeText => $"✓ {SubmittedAt:HH:mm}";

    /// <summary>For «Κατέβασμα» straight from the list (set where the form is at hand).</summary>
    public FormDefinition? Form { get; init; }
    public FormAssignment? Copy { get; init; }

    /// <summary>«Κατέβασμα»: the submitted copy's PDF and its file name.</summary>
    public (byte[] Bytes, string FileName)? CreatePdf()
    {
        if (Form is null || Copy is null) return null;
        return (AthloTrack.Services.FormPdf.WithAppFonts().Create(Form, Copy, AthleteName),
            AthloTrack.Services.FormPdf.FileName(Form, AthleteName));
    }

    /// <summary>«✓ σήμερα 18:42», «✓ χθες 21:03», «✓ 6/11».</summary>
    public string WhenText
    {
        get
        {
            var days = DateOnly.FromDateTime(DateTime.Today).DayNumber - DateOnly.FromDateTime(SubmittedAt.DateTime).DayNumber;
            return days switch
            {
                0 => $"✓ σήμερα {SubmittedAt:HH:mm}",
                1 => $"✓ χθες {SubmittedAt:HH:mm}",
                _ => $"✓ {SubmittedAt.ToString("d/M", CultureInfo.InvariantCulture)}",
            };
        }
    }
}
