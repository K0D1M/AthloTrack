using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

/// <summary>
/// An in-app "are you sure?" (the browser and Android heads have no native message box). For
/// actions that can't be undone, <see cref="RequiredText"/> must be typed before the button works.
/// </summary>
public sealed partial class ConfirmPrompt : ObservableObject
{
    private Func<Task>? _action;
    private Func<string?, Task>? _reasonAction;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    public partial string? Message { get; set; }

    [ObservableProperty]
    public partial string ButtonText { get; set; } = "OK";

    /// <summary>Destructive actions use the red button.</summary>
    [ObservableProperty]
    public partial bool IsDanger { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsText), nameof(TextHint))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial string? RequiredText { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial string Input { get; set; } = string.Empty;

    /// <summary>An optional free-text reason is asked too (e.g. «Δεν θα το κάνω»).</summary>
    [ObservableProperty]
    public partial bool WantsReason { get; set; }

    [ObservableProperty]
    public partial string Reason { get; set; } = string.Empty;

    public const int ReasonMaxLength = 200;

    public bool IsOpen => Message is not null;
    public bool NeedsText => !string.IsNullOrEmpty(RequiredText);
    public string TextHint => NeedsText ? $"Γράψε «{RequiredText}» για επιβεβαίωση" : string.Empty;

    public void Ask(string message, string button, Func<Task> action, bool danger = false, string? requiredText = null)
    {
        _action = action;
        _reasonAction = null;
        WantsReason = false;
        ButtonText = button;
        IsDanger = danger;
        Input = string.Empty;
        RequiredText = requiredText;
        Message = message;
    }

    /// <summary>Like <see cref="Ask"/>, with an optional reason; the action gets it trimmed, or null.</summary>
    public void AskWithReason(string message, string button, Func<string?, Task> action, bool danger = true)
    {
        _action = null;
        _reasonAction = action;
        ButtonText = button;
        IsDanger = danger;
        Input = string.Empty;
        RequiredText = null;
        Reason = string.Empty;
        WantsReason = true;
        Message = message;
    }

    internal static string? CleanReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return null;
        reason = reason.Trim();
        return reason.Length > ReasonMaxLength ? reason[..ReasonMaxLength] : reason;
    }

    private bool CanConfirm() =>
        !NeedsText || string.Equals(Input.Trim(), RequiredText, StringComparison.OrdinalIgnoreCase);

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmAsync()
    {
        var action = _action;
        var reasonAction = _reasonAction;
        var reason = CleanReason(Reason);
        Close();
        if (action is not null) await action();
        if (reasonAction is not null) await reasonAction(reason);
    }

    [RelayCommand]
    private void Cancel() => Close();

    private void Close()
    {
        _action = null;
        _reasonAction = null;
        WantsReason = false;
        Reason = string.Empty;
        Message = null;
        RequiredText = null;
        Input = string.Empty;
    }
}
