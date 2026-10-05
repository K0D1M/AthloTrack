using System;
using AthloTrack.Core.Auth;
using AthloTrack.ViewModels;
using Avalonia;
using Avalonia.Styling;

namespace AthloTrack.Services;

/// <summary>
/// The app's Light/Dark theme: Application.RequestedThemeVariant, with the choice kept in the
/// device's preferences (key <see cref="Key"/>). Views take their surface colours from the theme
/// tokens in Styles/Theme.axaml, so they follow the switch without reloading.
/// </summary>
public sealed class ThemeService : IThemeService
{
    public const string Key = "app.theme";

    private readonly IAppPreferences _preferences;

    public ThemeService(IAppPreferences preferences)
    {
        _preferences = preferences;
    }

    public ThemeChoice Current =>
        Enum.TryParse<ThemeChoice>(_preferences.Get(Key), out var choice) ? choice : ThemeChoice.System;

    public void Set(ThemeChoice choice)
    {
        _preferences.Set(Key, choice == ThemeChoice.System ? null : choice.ToString());
        Apply(choice);
    }

    /// <summary>At start-up, before the first view.</summary>
    public void ApplySaved() => Apply(Current);

    private static void Apply(ThemeChoice choice)
    {
        if (Application.Current is not { } app) return;
        app.RequestedThemeVariant = choice switch
        {
            ThemeChoice.Light => ThemeVariant.Light,
            ThemeChoice.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
