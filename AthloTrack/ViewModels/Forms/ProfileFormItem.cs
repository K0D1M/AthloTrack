using System;
using AthloTrack.Core.Models;

namespace AthloTrack.ViewModels;

/// <summary>A form on an athlete's profile: «Ιστορικό υγείας · Υποβλήθηκε 05/11» or «Εκκρεμεί».</summary>
public sealed class ProfileFormItem(FormAssignment assignment, string title)
{
    public Guid AssignmentId { get; } = assignment.Id;
    public string Title { get; } = title;
    public bool IsSubmitted { get; } = assignment.IsSubmitted;
    public string StatusText { get; } = assignment.SubmittedAtLocal is { } at ? $"Υποβλήθηκε {at:dd/MM}" : "Εκκρεμεί";
}
