using System;

namespace AthloTrack.ViewModels;

/// <summary>A form the athlete still has to fill in: «Έντυπο να συμπληρώσεις: Ιστορικό υγείας».</summary>
public sealed record OpenFormItem(Guid AssignmentId, string Title);
