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

    public bool IsOpen => Message is not null;
    public bool NeedsText => !string.IsNullOrEmpty(RequiredText);
    public string TextHint => NeedsText ? $"Γράψε «{RequiredText}» για επιβεβαίωση" : string.Empty;

    public void Ask(string message, string button, Func<Task> action, bool danger = false, string? requiredText = null)
    {
        _action = action;
        ButtonText = button;
        IsDanger = danger;
        Input = string.Empty;
        RequiredText = requiredText;
        Message = message;
    }

    private bool CanConfirm() =>
        !NeedsText || string.Equals(Input.Trim(), RequiredText, StringComparison.OrdinalIgnoreCase);

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmAsync()
    {
        var action = _action;
        Close();
        if (action is not null) await action();
    }

    [RelayCommand]
    private void Cancel() => Close();

    private void Close()
    {
        _action = null;
        Message = null;
        RequiredText = null;
        Input = string.Empty;
    }
}
