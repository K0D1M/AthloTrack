using AthloTrack.Core.Models;

namespace AthloTrack.ViewModels;

public sealed class WorkoutListItemViewModel
{
    public WorkoutListItemViewModel(WorkoutProgram program, string athleteName, bool showReadReceipt = false)
    {
        AthleteName = athleteName;
        TargetDateText = $"Ημ. στόχος: {program.TargetDate:dd/MM/yyyy}";
        Content = program.Content;
        IsCompleted = program.IsCompleted;
        CompletedText = program.CompletedAt is { } done ? $"✓ Ολοκληρώθηκε {done.ToLocalTime():dd/MM/yyyy}" : string.Empty;
        ShowReadReceipt = showReadReceipt && program.IsRead;
        ReadText = program.ReadAtLocal is { } read ? $"Διαβάστηκε από τον αθλητή στις {read:dd/MM/yyyy HH:mm}" : string.Empty;
    }

    public string AthleteName { get; }
    public string TargetDateText { get; }
    public string Content { get; }
    public bool IsCompleted { get; }
    public string CompletedText { get; }

    /// <summary>Coach only: the athlete has seen this workout.</summary>
    public bool ShowReadReceipt { get; }
    public string ReadText { get; }
}
