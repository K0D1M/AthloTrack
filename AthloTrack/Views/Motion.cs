using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Styling;

namespace AthloTrack.Views;

/// <summary>Short code-started animations (page changes); the style-driven ones are in Styles/Motion.axaml.</summary>
public static class Motion
{
    private static readonly Animation PageIn = Build(TimeSpan.FromMilliseconds(200), rise: 10);
    private static readonly Animation RootIn = Build(TimeSpan.FromMilliseconds(250), rise: 0);

    /// <summary>A newly shown page fades in and rises a little.</summary>
    public static void FadeInPage(Visual target) => _ = PageIn.RunAsync(target);

    /// <summary>A new root screen (login ↔ main) fades in.</summary>
    public static void FadeInRoot(Visual target) => _ = RootIn.RunAsync(target);

    private static Animation Build(TimeSpan duration, double rise)
    {
        var from = new KeyFrame { Cue = new Cue(0) };
        var to = new KeyFrame { Cue = new Cue(1) };
        from.Setters.Add(new Setter(Visual.OpacityProperty, 0d));
        to.Setters.Add(new Setter(Visual.OpacityProperty, 1d));
        if (rise > 0)
        {
            from.Setters.Add(new Setter(TranslateTransform.YProperty, rise));
            to.Setters.Add(new Setter(TranslateTransform.YProperty, 0d));
        }

        return new Animation
        {
            Duration = duration,
            Easing = new CubicEaseOut(),
            FillMode = FillMode.Backward,
            Children = { from, to },
        };
    }
}
