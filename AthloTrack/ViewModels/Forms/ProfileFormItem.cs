using System;
using AthloTrack.Core.Models;

namespace AthloTrack.ViewModels;

/// <summary>A form on an athlete's profile: «Ιστορικό υγείας · Υποβλήθηκε 05/11» or «Εκκρεμεί».</summary>
public sealed class ProfileFormItem(FormAssignment assignment, string title)
{
    public Guid AssignmentId { get; } = assignment.Id;
    public Guid FormId { get; } = assignment.FormId;
    public string Title { get; } = title;
    public bool IsSubmitted { get; } = assignment.IsSubmitted;
    public string StatusText { get; } = assignment.SubmittedAtLocal is { } at ? $"Υποβλήθηκε {at:dd/MM}" : "Εκκρεμεί";
}

/// <summary>One of the coach's forms in «Αποστολή εντύπου»; one the athlete has is greyed «Έχει σταλεί».</summary>
public sealed class FormSendChoice(FormDefinition form, bool alreadySent)
{
    public FormDefinition Form { get; } = form;
    public string Title => Form.Title;
    public bool AlreadySent { get; } = alreadySent;
    public bool CanSend => !AlreadySent;
    public double RowOpacity => AlreadySent ? 0.5 : 1;
    public string StateText => AlreadySent ? "Έχει σταλεί" : string.Empty;

    public string Hint
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
