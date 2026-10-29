using System;
using System.Globalization;
using System.Linq;
using AthloTrack.Core.Models;

namespace AthloTrack.ViewModels;

public sealed class WorkoutListItemViewModel
{
    private static readonly string[] ShortDays = ["Κυρ", "Δευ", "Τρί", "Τετ", "Πέμ", "Παρ", "Σάβ"];

    /// <param name="isCoach">Coach: sees read receipts and can remind. Athlete: answers open workouts.</param>
    /// <param name="athleteHasLogin">The athlete can receive a reminder.</param>
    /// <param name="today">For «Εκπρόθεσμο»; the device's date when not given.</param>
    public WorkoutListItemViewModel(WorkoutProgram program, string athleteName, bool isCoach = false, bool athleteHasLogin = false,
        DateOnly? today = null)
    {
        Program = program;
        AthleteName = athleteName;
        AthleteInitial = athleteName.Length > 0 ? athleteName.Substring(0, 1).ToUpperInvariant() : "?";
        IsCoach = isCoach;
        var day = today ?? DateOnly.FromDateTime(DateTime.Today);
        IsOverdue = !program.IsClosed && program.TargetDate < day;
        DayText = $"{ShortDays[(int)program.TargetDate.DayOfWeek]} {program.TargetDate.ToString("dd/MM", CultureInfo.InvariantCulture)}";
        TimeText = program.StartTime?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;
        (StatusText, StatusRank) = Status(program, isCoach, IsOverdue);
        Preview = PreviewOf(program.Content);
        ReasonText = program.HasDeclineReason ? $"Λόγος: {program.DeclineReason!.Trim()}" : string.Empty;
        MetaText = Meta(program, isCoach);
        CanEdit = isCoach;
        TargetDateText = $"Ημ. στόχος: {program.WhenText}";
        Content = program.Content;
        IsCompleted = program.IsCompleted;
        CompletedText = program.CompletedAt is { } done ? $"✓ Ολοκληρώθηκε {done.ToLocalTime():dd/MM/yyyy}" : string.Empty;
        IsNotCompleted = program.IsNotCompleted;
        NotCompletedText = program.NotCompletedAt is { } notDone ? $"✗ Δεν ολοκληρώθηκε {notDone.ToLocalTime():dd/MM/yyyy}" : string.Empty;
        ShowReadReceipt = isCoach && program.ShowRead;
        FromArchive = program.FromArchive;
        ReadText = program.ReadAtLocal is { } read ? $"Διαβάστηκε από τον αθλητή στις {read:dd/MM/yyyy HH:mm}" : string.Empty;
        CanComplete = !isCoach && program.CanAnswerOutcome;
        CanAccept = !isCoach && program.IsPending;
        CanRemind = isCoach && athleteHasLogin && !program.IsClosed;
        CanCancel = isCoach && program.CanBeCancelled;
        CanRestore = isCoach && program.IsCancelled;
        IsCancelled = program.IsCancelled;
        CancelledText = program.CancelledAtLocal is { } cancelled ? $"Ακυρώθηκε {cancelled:dd/MM}" : string.Empty;
        ShowAccepted = program.ShowAccepted;
        AcceptedText = isCoach ? "✓ Θα το κάνει" : "✓ Θα το κάνεις";
        ShowPending = isCoach && program.IsPending;
        IsDeclined = program.IsDeclined;
        DeclinedText = DeclineLine(program, isCoach);
        ShowReminderSent = isCoach && program.ShowReminderSent;
        ReminderText = program.LastRemindedAtLocal is { } at ? $"Υπενθύμιση στάλθηκε {at:dd/MM HH:mm}" : string.Empty;
    }

    /// <summary>The pill's text, and its place in «Κατάσταση» order (overdue first).</summary>
    private static (string Text, int Rank) Status(WorkoutProgram p, bool isCoach, bool overdue)
    {
        if (p.IsCancelled) return ("Ακυρώθηκε", 6);
        if (p.IsDeclined) return (isCoach ? "Δεν θα το κάνει" : "Δεν θα το κάνεις", 5);
        if (p.IsCompleted) return ("Ολοκληρώθηκε", 3);
        if (p.IsNotCompleted) return ("Δεν ολοκληρώθηκε", 4);
        if (overdue) return ("Εκπρόθεσμο", 0);
        if (p.IsAccepted) return (isCoach ? "Θα το κάνει" : "Θα το κάνεις", 2);
        return (isCoach ? "Αναμένει απάντηση" : "Νέο", 1);
    }

    /// <summary>The line under the program: when it was answered, and for the coach whether it was read.</summary>
    private static string Meta(WorkoutProgram p, bool isCoach)
    {
        if (p.FromArchive) return "Αρχείο · πριν από την εφαρμογή";
        var parts = new System.Collections.Generic.List<string>();
        if ((p.CompletedAtLocal ?? p.NotCompletedAtLocal) is { } answered) parts.Add($"Απάντηση {answered:dd/MM}");
        if (p.CancelledAtLocal is { } cancelled) parts.Add($"Ακυρώθηκε {cancelled:dd/MM}");
        if (isCoach && !p.IsCancelled)
            parts.Add(p.ReadAtLocal is { } read ? $"Διαβάστηκε {read:dd/MM HH:mm}" : "Δεν έχει διαβαστεί");
        return string.Join(" · ", parts);
    }

    /// <summary>The first line of the program without its markup («## Ζέσταμα» → «Ζέσταμα»).</summary>
    public static string PreviewOf(string content)
    {
        var line = content.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? string.Empty;
        line = line.TrimStart('#', '-', '*', '•', ' ').Replace("**", string.Empty);
        if (line.Length > 0 && char.IsDigit(line[0]))
        {
            var dot = line.IndexOf(". ", StringComparison.Ordinal);
            if (dot is > 0 and < 4) line = line[(dot + 2)..];
        }
        return line.Trim();
    }

    /// <summary>«✗ Δεν θα το κάνει: {reason}» (athlete: «κάνεις»).</summary>
    public static string DeclineLine(WorkoutProgram program, bool isCoach)
    {
        if (!program.IsDeclined) return string.Empty;
        var text = isCoach ? "✗ Δεν θα το κάνει" : "✗ Δεν θα το κάνεις";
        return program.HasDeclineReason ? $"{text}: {program.DeclineReason!.Trim()}" : text;
    }

    public WorkoutProgram Program { get; }
    public string AthleteName { get; }
    public string AthleteInitial { get; }
    public bool IsCoach { get; }

    /// <summary>A past day with no answer yet (and not cancelled or declined).</summary>
    public bool IsOverdue { get; }

    /// <summary>«Τρί 28/10», next to the calendar icon.</summary>
    public string DayText { get; }

    /// <summary>«18:30», next to the clock icon; empty without a time.</summary>
    public string TimeText { get; }
    public bool HasTime => TimeText.Length > 0;

    /// <summary>The coloured pill: «Αναμένει απάντηση», «Ολοκληρώθηκε», «Εκπρόθεσμο», …</summary>
    public string StatusText { get; }
    public int StatusRank { get; }

    // One of these is true: the pill's colour class.
    public bool StatusOverdue => StatusRank == 0;
    public bool StatusPending => StatusRank == 1;
    public bool StatusAccepted => StatusRank == 2;
    public bool StatusDone => StatusRank == 3;
    public bool StatusNotDone => StatusRank == 4;
    public bool StatusDeclined => StatusRank == 5;
    public bool StatusCancelled => StatusRank == 6;

    /// <summary>The program's first line, plain (the athlete's «Επόμενη προπόνηση» card).</summary>
    public string Preview { get; }

    /// <summary>«Λόγος: …» of a declined workout.</summary>
    public string ReasonText { get; }
    public bool HasReason => ReasonText.Length > 0;

    /// <summary>«Απάντηση 08/10 · Διαβάστηκε 14/10 00:23», «Αρχείο · πριν από την εφαρμογή».</summary>
    public string MetaText { get; }
    public bool HasMeta => MetaText.Length > 0;

    /// <summary>Coach: the pencil opens the workout form.</summary>
    public bool CanEdit { get; }
    public string TargetDateText { get; }
    public string Content { get; }
    public bool IsCompleted { get; }
    public string CompletedText { get; }
    public bool IsNotCompleted { get; }
    public string NotCompletedText { get; }

    /// <summary>Coach only: the athlete has seen this workout.</summary>
    public bool ShowReadReceipt { get; }

    /// <summary>«Αρχείο»: a workout from before the app.</summary>
    public bool FromArchive { get; }
    public string ReadText { get; }

    /// <summary>Athlete only: «Ολοκληρώθηκε» / «Δεν ολοκληρώθηκε» on an accepted, open workout.</summary>
    public bool CanComplete { get; }

    /// <summary>Athlete only: «Θα το κάνω» / «Δεν θα το κάνω» on a new workout.</summary>
    public bool CanAccept { get; }

    /// <summary>Coach only: «Ακύρωση» while it has no answer and wasn't declined.</summary>
    public bool CanCancel { get; }

    /// <summary>Coach only: «Επαναφορά» of a cancelled workout.</summary>
    public bool CanRestore { get; }

    public bool IsCancelled { get; }
    public string CancelledText { get; }
    public double CardOpacity => IsCancelled ? 0.55 : 1;

    public bool ShowAccepted { get; }
    public string AcceptedText { get; }

    /// <summary>Coach only: «Αναμένει απάντηση» before the athlete chooses.</summary>
    public bool ShowPending { get; }

    public bool IsDeclined { get; }
    public string DeclinedText { get; }

    /// <summary>Coach only: the reminder bell on an open workout of an athlete with a login.</summary>
    public bool CanRemind { get; }

    /// <summary>The bell works again an hour after the last reminder.</summary>
    public bool RemindEnabled => Program.CanBeReminded;

    public bool ShowReminderSent { get; }
    public string ReminderText { get; }
}
