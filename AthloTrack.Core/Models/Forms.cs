using System.Globalization;

namespace AthloTrack.Core.Models;

/// <summary>A question's kind in an «Έντυπο» (stored as its lower-case name).</summary>
public enum FormQuestionKind { Short, Long, YesNo, Choice, Date }

/// <summary>One question of a form: what is asked, how it is answered, and whether it must be.</summary>
public sealed class FormQuestion
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public FormQuestionKind Kind { get; set; } = FormQuestionKind.Short;
    public string Label { get; set; } = string.Empty;
    public bool Required { get; set; }

    /// <summary><see cref="FormQuestionKind.Choice"/>: the options, in order.</summary>
    public List<string> Options { get; set; } = new();

    /// <summary>«Σύντομη απάντηση», «Κείμενο», …: the kind's name in the editor.</summary>
    public static string KindName(FormQuestionKind kind) => kind switch
    {
        FormQuestionKind.Long => "Κείμενο",
        FormQuestionKind.YesNo => "Ναι / Όχι",
        FormQuestionKind.Choice => "Επιλογή",
        FormQuestionKind.Date => "Ημερομηνία",
        _ => "Σύντομη απάντηση",
    };
}

/// <summary>«Έντυπο»: the coach's form, sent to chosen athletes.</summary>
public sealed class FormDefinition
{
    public Guid Id { get; set; }
    public Guid CoachId { get; set; }

    /// <summary>The coach's name, kept by the database (the PDF's author).</summary>
    public string CoachName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    /// <summary>The formatted text before the questions (the Ασκησιολόγιο markup).</summary>
    public string Intro { get; set; } = string.Empty;
    public List<FormQuestion> Questions { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>One athlete's copy of a form: empty until they submit it, then final.</summary>
public sealed class FormAssignment
{
    public Guid Id { get; set; }
    public Guid FormId { get; set; }
    public Guid AthleteId { get; set; }

    /// <summary>Question id → answer; empty until submitted.</summary>
    public Dictionary<string, string> Answers { get; set; } = new();
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public bool IsSubmitted => SubmittedAt is not null;
    public DateTimeOffset? SubmittedAtLocal => SubmittedAt?.ToLocalTime();
}

/// <summary>Answers: what counts as given, and how one reads (Ναι / Όχι, dd/MM/yyyy).</summary>
public static class FormAnswers
{
    public const string Yes = "yes";
    public const string No = "no";

    /// <summary>The required questions left empty.</summary>
    public static IReadOnlyList<FormQuestion> Missing(IEnumerable<FormQuestion> questions, IReadOnlyDictionary<string, string> answers) =>
        questions.Where(q => q.Required && string.IsNullOrWhiteSpace(answers.GetValueOrDefault(q.Id))).ToList();

    /// <summary>The answer as it is shown and printed; «—» when an optional question was left empty.</summary>
    public static string Display(FormQuestion question, string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer)) return "—";
        return question.Kind switch
        {
            FormQuestionKind.YesNo => answer == Yes ? "Ναι" : answer == No ? "Όχι" : answer,
            FormQuestionKind.Date => DateOnly.TryParseExact(answer, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : answer,
            _ => answer.Trim(),
        };
    }
}
