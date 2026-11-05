using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;
using AthloTrack.Core.Forms;
using AthloTrack.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

/// <summary>
/// «Νέο έντυπο» / «Επεξεργασία εντύπου»: the title, the formatted intro text (typed, pasted or
/// imported from a .docx) and the questions. Once an athlete has submitted it, the questions are
/// locked (the database refuses the change too); the title and text stay editable.
/// </summary>
public partial class FormEditorViewModel : ViewModelBase
{
    private readonly IFormRepository _forms;
    private readonly SessionState _session;
    private readonly FormDefinition? _existing;

    public FormEditorViewModel(IFormRepository forms, SessionState session, FormDefinition? existing = null, bool locked = false)
    {
        _forms = forms;
        _session = session;
        _existing = existing;
        IsLocked = locked;
        if (existing is not null)
        {
            Title = existing.Title;
            Intro = existing.Intro;
            foreach (var q in existing.Questions) Questions.Add(FormQuestionEditor.From(q));
        }
    }

    public event Action<FormDefinition>? Saved;
    public event Action? Cancelled;

    public ConfirmPrompt Confirm { get; } = new();

    public bool IsNew => _existing is null;
    public string PageTitle => IsNew ? "Νέο έντυπο" : "Επεξεργασία εντύπου";

    /// <summary>Someone has submitted it: the questions can't change.</summary>
    public bool IsLocked { get; }
    public bool CanEditQuestions => !IsLocked;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Intro { get; set; } = string.Empty;

    public ObservableCollection<FormQuestionEditor> Questions { get; } = new();

    public static IReadOnlyList<string> KindNames { get; } =
        Enum.GetValues<FormQuestionKind>().Select(FormQuestion.KindName).ToList();

    [ObservableProperty]
    public partial string? FormError { get; set; }

    [ObservableProperty]
    public partial string? InfoMessage { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [RelayCommand]
    private void AddQuestion()
    {
        if (IsLocked) return;
        Questions.Add(new FormQuestionEditor());
    }

    [RelayCommand]
    private void RemoveQuestion(FormQuestionEditor? q)
    {
        if (!IsLocked && q is not null) Questions.Remove(q);
    }

    [RelayCommand]
    private void MoveUp(FormQuestionEditor? q) => Move(q, -1);

    [RelayCommand]
    private void MoveDown(FormQuestionEditor? q) => Move(q, 1);

    private void Move(FormQuestionEditor? q, int by)
    {
        if (IsLocked || q is null) return;
        var i = Questions.IndexOf(q);
        var j = i + by;
        if (i < 0 || j < 0 || j >= Questions.Count) return;
        Questions.Move(i, j);
    }

    /// <summary>«Εισαγωγή από Word»: the .docx's text, keeping headings, lists and bold.</summary>
    public void ImportDocx(Stream docx)
    {
        FormError = null;
        string markup;
        try
        {
            markup = DocxImport.ToMarkup(docx);
        }
        catch (Exception ex)
        {
            FormError = ex is InvalidDataException ? ex.Message : $"Το αρχείο Word δεν διαβάστηκε: {ex.Message}";
            return;
        }

        if (markup.Length == 0)
        {
            FormError = "Το αρχείο Word δεν έχει κείμενο.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Intro))
        {
            Intro = markup;
            InfoMessage = "Το κείμενο του Word μπήκε στο κείμενο του εντύπου.";
            return;
        }

        Confirm.Ask("Το κείμενο του Word θα αντικαταστήσει το κείμενο που έχει ήδη το έντυπο.", "Αντικατάσταση", () =>
        {
            Intro = markup;
            InfoMessage = "Το κείμενο αντικαταστάθηκε από το Word.";
            return Task.CompletedTask;
        });
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();

    [RelayCommand]
    private async Task SaveAsync()
    {
        FormError = Validate();
        if (FormError is not null) return;

        IsBusy = true;
        try
        {
            var form = new FormDefinition
            {
                Id = _existing?.Id ?? Guid.Empty,
                CoachId = _existing?.CoachId ?? _session.ProfileId ?? Guid.Empty,
                Title = Title.Trim(),
                Intro = Intro.TrimEnd(),
                Questions = Questions.Select(q => q.ToModel()).ToList(),
            };
            var saved = await _forms.SaveFormAsync(form);
            Saved?.Invoke(saved);
        }
        catch (Exception ex)
        {
            FormError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>The first problem to fix, or null.</summary>
    internal string? Validate()
    {
        foreach (var q in Questions) q.Error = null;
        if (string.IsNullOrWhiteSpace(Title)) return "Δώσε τίτλο στο έντυπο.";
        if (Questions.Count == 0 && string.IsNullOrWhiteSpace(Intro)) return "Γράψε κείμενο ή πρόσθεσε τουλάχιστον μία ερώτηση.";
        foreach (var q in Questions)
        {
            if (string.IsNullOrWhiteSpace(q.Label)) q.Error = "Γράψε την ερώτηση.";
            else if (q.Kind == FormQuestionKind.Choice && q.OptionList.Count < 2) q.Error = "Γράψε τουλάχιστον δύο επιλογές, μία σε κάθε γραμμή.";
        }
        return Questions.Any(q => q.Error is not null) ? "Διόρθωσε τις ερωτήσεις με κόκκινο." : null;
    }
}

/// <summary>One question in the editor.</summary>
public sealed partial class FormQuestionEditor : ObservableObject
{
    public string Id { get; private init; } = Guid.NewGuid().ToString("N")[..8];

    [ObservableProperty]
    public partial string Label { get; set; } = string.Empty;

    /// <summary>The kind's place in <see cref="FormEditorViewModel.KindNames"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Kind), nameof(IsChoice))]
    public partial int KindIndex { get; set; }

    public FormQuestionKind Kind => (FormQuestionKind)Math.Clamp(KindIndex, 0, 4);
    public bool IsChoice => Kind == FormQuestionKind.Choice;

    [ObservableProperty]
    public partial bool Required { get; set; }

    /// <summary>«Επιλογή»: one option a line.</summary>
    [ObservableProperty]
    public partial string OptionsText { get; set; } = string.Empty;

    public List<string> OptionList =>
        OptionsText.Replace("\r", string.Empty).Split('\n').Select(o => o.Trim()).Where(o => o.Length > 0).ToList();

    [ObservableProperty]
    public partial string? Error { get; set; }

    public static FormQuestionEditor From(FormQuestion q) => new()
    {
        Id = q.Id,
        Label = q.Label,
        KindIndex = (int)q.Kind,
        Required = q.Required,
        OptionsText = string.Join('\n', q.Options),
    };

    public FormQuestion ToModel() => new()
    {
        Id = Id,
        Kind = Kind,
        Label = Label.Trim(),
        Required = Required,
        Options = IsChoice ? OptionList : new(),
    };
}
