using System;
using AthloTrack.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

public enum PinPadMode
{
    /// <summary>Unlocking the app («Γεια σου, …»).</summary>
    Unlock,

    /// <summary>Choosing a new PIN, typed twice.</summary>
    Create,

    /// <summary>Ρυθμίσεις: the current PIN before turning it off or changing it.</summary>
    ConfirmCurrent,
}

/// <summary>
/// The PIN keypad: six dots, 0–9 and ⌫. The 6th digit submits. No text box, so the phone's
/// keyboard never opens.
/// </summary>
public partial class PinPadViewModel : ViewModelBase
{
    private readonly PinLock _pin;
    private string _entered = string.Empty;
    private string? _firstEntry;

    public PinPadViewModel(PinLock pin, PinPadMode mode, string? displayName = null)
    {
        _pin = pin;
        Mode = mode;
        _displayName = displayName;
        ResetTexts();
    }

    private readonly string? _displayName;

    public PinPadMode Mode { get; }

    /// <summary>Unlocked (Unlock) or the current PIN was right (ConfirmCurrent).</summary>
    public event Action? Unlocked;

    /// <summary>A new PIN was typed twice the same.</summary>
    public event Action<string>? Created;

    public event Action? Cancelled;

    /// <summary>«Ξέχασα το PIN» (false) or too many wrong PINs (true): sign in with the password.</summary>
    public event Action<bool>? SignOutRequested;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Subtitle { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    /// <summary>Flipped on a wrong PIN, for the view's shake.</summary>
    [ObservableProperty]
    public partial int ShakeCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Dot1), nameof(Dot2), nameof(Dot3), nameof(Dot4), nameof(Dot5), nameof(Dot6))]
    public partial int Filled { get; set; }

    public bool Dot1 => Filled >= 1;
    public bool Dot2 => Filled >= 2;
    public bool Dot3 => Filled >= 3;
    public bool Dot4 => Filled >= 4;
    public bool Dot5 => Filled >= 5;
    public bool Dot6 => Filled >= 6;

    public bool HasSubtitle => !string.IsNullOrEmpty(Subtitle);
    public bool CanCancel => Mode != PinPadMode.Unlock;
    public bool ShowForgot => Mode != PinPadMode.Create;

    partial void OnSubtitleChanged(string? value) => OnPropertyChanged(nameof(HasSubtitle));

    [RelayCommand]
    public void Digit(string? digit)
    {
        if (digit is not { Length: 1 } || digit[0] is < '0' or > '9' || _entered.Length >= PinLock.Length) return;
        Error = null;
        _entered += digit;
        Filled = _entered.Length;
        if (_entered.Length == PinLock.Length) Submit();
    }

    [RelayCommand]
    public void Backspace()
    {
        if (_entered.Length == 0) return;
        _entered = _entered[..^1];
        Filled = _entered.Length;
    }

    [RelayCommand]
    private void Cancel()
    {
        if (CanCancel) Cancelled?.Invoke();
    }

    [RelayCommand]
    private void Forgot() => SignOutRequested?.Invoke(false);

    private void Submit()
    {
        var entry = _entered;
        Clear();

        if (Mode == PinPadMode.Create)
        {
            if (_firstEntry is null)
            {
                _firstEntry = entry;
                Subtitle = "Ξανά, για επιβεβαίωση";
                return;
            }
            if (_firstEntry == entry)
            {
                Created?.Invoke(entry);
                return;
            }
            _firstEntry = null;
            ResetTexts();
            Fail("Τα PIN δεν ταιριάζουν. Ξεκίνα από την αρχή.");
            return;
        }

        var check = _pin.Verify(entry);
        switch (check.Result)
        {
            case PinCheckResult.Ok:
                Unlocked?.Invoke();
                break;
            case PinCheckResult.LockedOut:
                SignOutRequested?.Invoke(true);
                break;
            default:
                Fail(check.Remaining == 1
                    ? "Λάθος PIN· απομένει 1 προσπάθεια."
                    : $"Λάθος PIN· απομένουν {check.Remaining} προσπάθειες.");
                break;
        }
    }

    private void Fail(string message)
    {
        Error = message;
        ShakeCount++;
    }

    private void Clear()
    {
        _entered = string.Empty;
        Filled = 0;
    }

    private void ResetTexts()
    {
        switch (Mode)
        {
            case PinPadMode.Unlock:
                Title = string.IsNullOrWhiteSpace(_displayName) ? "Γεια σου" : $"Γεια σου, {_displayName}";
                Subtitle = null;
                break;
            case PinPadMode.Create:
                Title = "Νέο PIN";
                Subtitle = "Διάλεξε 6 ψηφία";
                break;
            default:
                Title = "Τρέχον PIN";
                Subtitle = null;
                break;
        }
    }
}
