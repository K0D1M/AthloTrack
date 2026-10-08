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
        CanComplete = !isCoach && !program.IsAnswered;
        CanRemind = isCoach && athleteHasLogin && !program.IsAnswered;
        ShowReminderSent = isCoach && program.ShowReminderSent;
        ReminderText = program.LastRemindedAtLocal is { } at ? $"Υπενθύμιση στάλθηκε {at:dd/MM HH:mm}" : string.Empty;
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

    /// <summary>Athlete only: «Ολοκληρώθηκε» / «Δεν ολοκληρώθηκε» on an unanswered workout.</summary>
    public bool CanComplete { get; }

    /// <summary>Coach only: the reminder bell on an unanswered workout of an athlete with a login.</summary>
    public bool CanRemind { get; }

    /// <summary>The bell works again an hour after the last reminder.</summary>
    public bool RemindEnabled => Program.CanBeReminded;

    public bool ShowReminderSent { get; }
    public string ReminderText { get; }
}
