using System;
using System.Threading.Tasks;
using AthloTrack.ViewModels;

namespace AthloTrack.Services;

/// <summary>
/// Ρυθμίσεις asks for the PIN keypad (to create a PIN, or confirm the current one); the app
/// shows it over everything (<see cref="Host"/> is set by <c>App</c>).
/// </summary>
public sealed class PinPrompt
{
    /// <summary>Shows the keypad; completes with the new PIN (Create), "ok" (ConfirmCurrent), or null if cancelled.</summary>
    public Func<PinPadMode, Task<string?>>? Host { get; set; }

    public Task<string?> ShowAsync(PinPadMode mode) => Host?.Invoke(mode) ?? Task.FromResult<string?>(null);
}
