using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AthloTrack.Core.Data;
using AthloTrack.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

/// <summary>
/// A form's page (coach): who it went to and who has submitted it, and the form's actions:
/// «Αποστολή σε…», «Επεξεργασία», «Αντίγραφο», «Διαγραφή». A submitted copy opens read-only.
/// </summary>
public partial class FormDetailViewModel : ViewModelBase
{
    private readonly IFormRepository _forms;
    private readonly IAthleteRepository _athletes;

    public FormDetailViewModel(IFormRepository forms, IAthleteRepository athletes, FormDefinition form, string? infoMessage = null)
    {
        _forms = forms;
        _athletes = athletes;
        Form = form;
        InfoMessage = infoMessage;
        _ = LoadAsync();
    }

    public FormDefinition Form { get; private set; }
    public string Title => Form.Title;

    public event Action<FormDefinition, bool>? EditRequested;
    public event Action<FormDefinition>? SendRequested;
    public event Action<FormAssignment, FormDefinition, string>? OpenAnswersRequested;
    public event Action<FormDefinition>? CopyCreated;
    public event Action? Deleted;

    public ConfirmPrompt Confirm { get; } = new();

    public ObservableCollection<FormRecipientItem> Recipients { get; } = new();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? InfoMessage { get; set; }

    [ObservableProperty]
    public partial string Summary { get; set; } = string.Empty;

    /// <summary>Someone has submitted it: the questions are locked.</summary>
    public bool IsLocked => Recipients.Any(r => r.IsSubmitted);

    public bool HasRecipients => Recipients.Count > 0;

    public string QuestionsText => Form.Questions.Count == 1 ? "1 ερώτηση" : $"{Form.Questions.Count} ερωτήσεις";

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var names = (await _athletes.GetAllAsync()).ToDictionary(a => a.Id, a => a.FullName);
            var copies = await _forms.GetAssignmentsAsync(Form.Id);
            Recipients.Clear();
            foreach (var a in copies.OrderBy(a => a.IsSubmitted).ThenBy(a => names.GetValueOrDefault(a.AthleteId)))
                Recipients.Add(new FormRecipientItem(a, names.GetValueOrDefault(a.AthleteId) ?? "—"));
            var submitted = copies.Count(a => a.IsSubmitted);
            Summary = copies.Count == 0 ? "Δεν έχει σταλεί ακόμη σε κανέναν αθλητή." : $"Υποβλήθηκαν {submitted} από {copies.Count}";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsLocked));
            OnPropertyChanged(nameof(HasRecipients));
        }
    }

    [RelayCommand]
    private void Send() => SendRequested?.Invoke(Form);

    [RelayCommand]
    private void Edit() => EditRequested?.Invoke(Form, IsLocked);

    [RelayCommand]
    private void OpenAnswers(FormRecipientItem? item)
    {
        if (item is { IsSubmitted: true }) OpenAnswersRequested?.Invoke(item.Assignment, Form, item.AthleteName);
    }

    /// <summary>«Αντίγραφο»: the same form, new and editable, not sent to anyone.</summary>
    [RelayCommand]
    private async Task CopyAsync()
    {
        ErrorMessage = null;
        try
        {
            var copy = await _forms.SaveFormAsync(new FormDefinition
            {
                CoachId = Form.CoachId,
                Title = $"{Form.Title} (αντίγραφο)",
                Intro = Form.Intro,
                Questions = Form.Questions.Select(q => new FormQuestion
                {
                    Id = q.Id, Kind = q.Kind, Label = q.Label, Required = q.Required, Options = q.Options.ToList(),
                }).ToList(),
            });
            CopyCreated?.Invoke(copy);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Delete()
    {
        var answered = Recipients.Count(r => r.IsSubmitted);
        var message = answered == 0
            ? $"Να διαγραφεί το έντυπο «{Form.Title}»;"
            : $"Να διαγραφεί το έντυπο «{Form.Title}»; Θα διαγραφούν και οι απαντήσεις {answered} αθλητών.";
        Confirm.Ask(message, "Διαγραφή", async () =>
        {
            try
            {
                await _forms.DeleteFormAsync(Form.Id);
                Deleted?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }, danger: true);
    }
}

/// <summary>An athlete the form went to: «Εκκρεμεί» or «Υποβλήθηκε 05/11».</summary>
public sealed class FormRecipientItem(FormAssignment assignment, string athleteName)
{
    public FormAssignment Assignment { get; } = assignment;
    public string AthleteName { get; } = athleteName;
    public string Initial => AthleteName.Length > 0 ? AthleteName[..1].ToUpperInvariant() : "?";
    public bool IsSubmitted => Assignment.IsSubmitted;
    public string StatusText => Assignment.SubmittedAtLocal is { } at ? $"Υποβλήθηκε {at:dd/MM HH:mm}" : "Εκκρεμεί";
}
