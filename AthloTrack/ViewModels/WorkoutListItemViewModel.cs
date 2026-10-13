using AthloTrack.Core.Models;

namespace AthloTrack.ViewModels;

public sealed class WorkoutListItemViewModel
{
    /// <param name="isCoach">Coach: sees read receipts and can remind. Athlete: answers open workouts.</param>
    /// <param name="athleteHasLogin">The athlete can receive a reminder.</param>
    public WorkoutListItemViewModel(WorkoutProgram program, string athleteName, bool isCoach = false, bool athleteHasLogin = false)
    {
        Program = program;
        AthleteName = athleteName;
        TargetDateText = $"Ημ. στόχος: {program.TargetDate:dd/MM/yyyy}";
        Content = program.Content;
        IsCompleted = program.IsCompleted;
        CompletedText = program.CompletedAt is { } done ? $"✓ Ολοκληρώθηκε {done.ToLocalTime():dd/MM/yyyy}" : string.Empty;
        IsNotCompleted = program.IsNotCompleted;
        NotCompletedText = program.NotCompletedAt is { } notDone ? $"✗ Δεν ολοκληρώθηκε {notDone.ToLocalTime():dd/MM/yyyy}" : string.Empty;
        ShowReadReceipt = isCoach && program.IsRead;
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

    /// <summary>«✗ Δεν θα το κάνει: {reason}» (athlete: «κάνεις»).</summary>
    public static string DeclineLine(WorkoutProgram program, bool isCoach)
    {
        if (!program.IsDeclined) return string.Empty;
        var text = isCoach ? "✗ Δεν θα το κάνει" : "✗ Δεν θα το κάνεις";
        return program.HasDeclineReason ? $"{text}: {program.DeclineReason!.Trim()}" : text;
    }

    public WorkoutProgram Program { get; }
    public string AthleteName { get; }
    public string TargetDateText { get; }
    public string Content { get; }
    public bool IsCompleted { get; }
    public string CompletedText { get; }
    public bool IsNotCompleted { get; }
    public string NotCompletedText { get; }

    /// <summary>Coach only: the athlete has seen this workout.</summary>
    public bool ShowReadReceipt { get; }
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
