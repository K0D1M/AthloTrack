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
using Newtonsoft.Json.Linq;

namespace AthloTrack.ViewModels;

/// <summary>
/// One athlete's copy of a form. The athlete fills it in (a draft stays on the device while they
/// type, so the coach never sees half-done answers) and submits it once; after that it, like
/// the coach's view, shows the answers read-only with «Λήψη PDF».
/// </summary>
public partial class FormFillViewModel : ViewModelBase
{
    private readonly IFormRepository _forms;
    private readonly IAthleteRepository _athletes;
    private readonly SessionState _session;
    private readonly IAppPreferences _preferences;
    private readonly Guid _assignmentId;
    private bool _restoring;

    public FormFillViewModel(IFormRepository forms, IAthleteRepository athletes, SessionState session,
        Guid assignmentId, IAppPreferences? preferences = null)
    {
        _forms = forms;
        _athletes = athletes;
        _session = session;
        _assignmentId = assignmentId;
        _preferences = preferences ?? new InMemoryAppPreferences();
        _ = LoadAsync();
    }

    private string DraftKey => $"form.draft.{_assignmentId}";

    public ConfirmPrompt Confirm { get; } = new();

    public FormDefinition? Form { get; private set; }
    public FormAssignment? Assignment { get; private set; }

    [ObservableProperty]
    public partial string Title { get; set; } = "Έντυπο";

    [ObservableProperty]
    public partial string Intro { get; set; } = string.Empty;

    public bool HasIntro => !string.IsNullOrWhiteSpace(Intro);

    [ObservableProperty]
    public partial string AthleteName { get; set; } = string.Empty;

    public ObservableCollection<FormAnswerItem> Items { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit), nameof(CanSubmit))]
    public partial bool IsReadOnly { get; set; } = true;

    public bool CanEdit => !IsReadOnly;

    /// <summary>«Υποβλήθηκε 05/11/2026 18:30».</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSubmitted))]
    public partial string SubmittedText { get; set; } = string.Empty;

    public bool IsSubmitted => SubmittedText.Length > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    public partial bool IsBusy { get; set; }

    public bool CanSubmit => !IsReadOnly && !IsBusy;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? FormError { get; set; }

    [ObservableProperty]
    public partial bool HasDraft { get; set; }

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            Assignment = await _forms.GetAssignmentAsync(_assignmentId)
                         ?? throw new InvalidOperationException("Το έντυπο δεν βρέθηκε (ίσως διαγράφηκε).");
            Form = await _forms.GetFormAsync(Assignment.FormId)
                   ?? throw new InvalidOperationException("Το έντυπο δεν βρέθηκε (ίσως διαγράφηκε).");
            AthleteName = (await _athletes.GetAllAsync()).FirstOrDefault(a => a.Id == Assignment.AthleteId)?.FullName ?? string.Empty;
            Title = Form.Title;
            Intro = Form.Intro;
            OnPropertyChanged(nameof(HasIntro));

            // Only the athlete fills it in, and only once.
            IsReadOnly = Assignment.IsSubmitted || _session.IsCoach || _session.ProfileId != Assignment.AthleteId;
            SubmittedText = Assignment.SubmittedAtLocal is { } at
                ? $"Υποβλήθηκε {at.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)}"
                : string.Empty;

            var answers = Assignment.IsSubmitted ? Assignment.Answers : Draft();
            HasDraft = !Assignment.IsSubmitted && answers.Count > 0;
            _restoring = true;
            Items.Clear();
            var number = 0;
            foreach (var q in Form.Questions)
            {
                var item = new FormAnswerItem(q, ++number, IsReadOnly);
                item.Value = answers.GetValueOrDefault(q.Id);
                item.Changed += SaveDraft;
                Items.Add(item);
            }
            _restoring = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private Dictionary<string, string> Draft()
    {
        try
        {
            var stored = _preferences.Get(DraftKey);
            return stored is null ? new() : JObject.Parse(stored).Properties().ToDictionary(p => p.Name, p => p.Value.ToString());
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return new();
        }
    }

    private void SaveDraft()
    {
        if (_restoring || IsReadOnly) return;
        var json = new JObject();
        foreach (var i in Items)
            if (!string.IsNullOrEmpty(i.Value)) json[i.Question.Id] = i.Value;
        _preferences.Set(DraftKey, json.HasValues ? json.ToString(Newtonsoft.Json.Formatting.None) : null);
        HasDraft = json.HasValues;
    }

    private Dictionary<string, string> Answers() =>
        Items.Where(i => !string.IsNullOrWhiteSpace(i.Value)).ToDictionary(i => i.Question.Id, i => i.Value!.Trim());

    [RelayCommand]
    private void Submit()
    {
        if (!CanSubmit || Form is null) return;
        FormError = null;
        var answers = Answers();
        var missing = FormAnswers.Missing(Form.Questions, answers);
        foreach (var i in Items) i.IsMissing = missing.Contains(i.Question);
        if (missing.Count > 0)
        {
            FormError = missing.Count == 1
                ? "Συμπλήρωσε την υποχρεωτική ερώτηση με κόκκινο."
                : $"Συμπλήρωσε τις {missing.Count} υποχρεωτικές ερωτήσεις με κόκκινο.";
            return;
        }

        Confirm.Ask("Σίγουρα; Μετά την υποβολή οι απαντήσεις δεν αλλάζουν. Τις βλέπει μόνο ο προπονητής σου.", "Υποβολή", async () =>
        {
            IsBusy = true;
            try
            {
                await _forms.SubmitAsync(_assignmentId, answers);
                _preferences.Set(DraftKey, null);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                FormError = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        });
    }

    /// <summary>«Λήψη PDF»: the submitted copy as a formal PDF, and its suggested file name.</summary>
    public (byte[] Bytes, string FileName)? CreatePdf()
    {
        if (Form is null || Assignment is not { IsSubmitted: true }) return null;
        var pdf = AthloTrack.Services.FormPdf.WithAppFonts();
        return (pdf.Create(Form, Assignment, AthleteName), AthloTrack.Services.FormPdf.FileName(Form, AthleteName));
    }
}

/// <summary>One question on the fill-in page, with the right input for its kind.</summary>
public sealed partial class FormAnswerItem : ObservableObject
{
    public FormAnswerItem(FormQuestion question, int number, bool readOnly)
    {
        Question = question;
        Number = number;
        IsReadOnly = readOnly;
    }

    public FormQuestion Question { get; }
    public int Number { get; }
    public bool IsReadOnly { get; }
    public bool IsEditable => !IsReadOnly;

    public string Label => $"{Number}. {Question.Label}";
    public bool IsRequired => Question.Required;

    public bool IsShort => IsEditable && Question.Kind == FormQuestionKind.Short;
    public bool IsLong => IsEditable && Question.Kind == FormQuestionKind.Long;
    public bool IsYesNo => IsEditable && Question.Kind == FormQuestionKind.YesNo;
    public bool IsChoice => IsEditable && Question.Kind == FormQuestionKind.Choice;
    public bool IsDate => IsEditable && Question.Kind == FormQuestionKind.Date;
    public IReadOnlyList<string> Options => Question.Options;

    /// <summary>Raised on every change, for the device draft.</summary>
    public event Action? Changed;

    private string? _value;

    /// <summary>The answer as stored: text, "yes"/"no", the option, or yyyy-MM-dd.</summary>
    public string? Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(IsYes));
            OnPropertyChanged(nameof(IsNo));
            OnPropertyChanged(nameof(SelectedOption));
            OnPropertyChanged(nameof(Date));
            OnPropertyChanged(nameof(Display));
            if (!string.IsNullOrWhiteSpace(value)) IsMissing = false;
            Changed?.Invoke();
        }
    }

    public string? Text
    {
        get => _value;
        set => Value = value;
    }

    public bool IsYes
    {
        get => _value == FormAnswers.Yes;
        set { if (value) Value = FormAnswers.Yes; }
    }

    public bool IsNo
    {
        get => _value == FormAnswers.No;
        set { if (value) Value = FormAnswers.No; }
    }

    public string? SelectedOption
    {
        get => Question.Options.Contains(_value ?? string.Empty) ? _value : null;
        set => Value = value;
    }

    public DateTimeOffset? Date
    {
        get => DateOnly.TryParseExact(_value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue))
            : null;
        set => Value = value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    /// <summary>Read-only: «Ναι», «12/03/1998», «—».</summary>
    public string Display => FormAnswers.Display(Question, _value);

    [ObservableProperty]
    public partial bool IsMissing { get; set; }
}
