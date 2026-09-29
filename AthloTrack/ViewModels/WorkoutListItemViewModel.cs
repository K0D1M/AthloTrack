using AthloTrack.Core.Models;

namespace AthloTrack.ViewModels;

public sealed class WorkoutListItemViewModel
{
    public WorkoutListItemViewModel(WorkoutProgram program, string athleteName)
    {
        AthleteName = athleteName;
        TargetDateText = $"Ημ. στόχος: {program.TargetDate:dd/MM/yyyy}";
        Content = program.Content;
    }

    public string AthleteName { get; }
    public string TargetDateText { get; }
    public string Content { get; }
}
