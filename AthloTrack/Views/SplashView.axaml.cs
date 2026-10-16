using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace AthloTrack.Views;

/// <summary>
/// The opening screen, shown over the first real screen while the saved session is restored.
/// <see cref="Play"/> starts the heartbeat and the loading bar; <see cref="DismissAsync"/> moves the
/// logo onto the login screen's logo (or just fades out over the main screen / PIN lock).
/// </summary>
public partial class SplashView : UserControl
{
    private const double LogoSize = 112;
    private static readonly TimeSpan MoveTime = TimeSpan.FromMilliseconds(650);
    private static readonly Easing Ease = new SplineEasing(0.2, 0.8, 0.2, 1);

    private readonly CancellationTokenSource _beat = new();
    private Thickness _safeArea;

    public SplashView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Place();
    }

    /// <summary>Keeps the name, motto and version clear of the system bars (the backdrop and logo aren't).</summary>
    public void SetSafeArea(Thickness padding)
    {
        _safeArea = padding;
        Texts.Margin = padding;
        Place();
    }

    /// <summary>The logo in the centre of the whole window, the name below it.</summary>
    private void Place()
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0) return;
        Canvas.SetLeft(Logo, (size.Width - LogoSize) / 2);
        Canvas.SetTop(Logo, (size.Height - LogoSize) / 2);
        Heading.Margin = new Thickness(0, size.Height / 2 + LogoSize / 2 + 32 - _safeArea.Top, 0, 0);
    }

    /// <summary>Starts the heartbeat and fills the loading bar over <paramref name="duration"/>.</summary>
    public void Play(TimeSpan duration)
    {
        _ = Heartbeat().RunAsync(Heart, _beat.Token);
        _ = Fill(duration).RunAsync(BarFill);
    }

    /// <summary>
    /// Ends the splash. With <paramref name="target"/> (the login screen's logo) the logo moves onto
    /// it while the rest fades, so the login screen seems to grow out of the splash.
    /// </summary>
    public async Task DismissAsync(Control? target)
    {
        _beat.Cancel();
        var fade = TimeSpan.FromMilliseconds(500);
        Backdrop.Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = fade, Easing = Ease }];
        Texts.Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(300) }];

        if (target is { IsEffectivelyVisible: true } && target.TranslatePoint(new Point(0, 0), this) is { } at
            && target.Bounds.Width > 0)
        {
            Logo.Transitions =
            [
                new DoubleTransition { Property = Canvas.LeftProperty, Duration = MoveTime, Easing = Ease },
                new DoubleTransition { Property = Canvas.TopProperty, Duration = MoveTime, Easing = Ease },
                new DoubleTransition { Property = WidthProperty, Duration = MoveTime, Easing = Ease },
                new DoubleTransition { Property = HeightProperty, Duration = MoveTime, Easing = Ease },
                new CornerRadiusTransition { Property = Border.CornerRadiusProperty, Duration = MoveTime, Easing = Ease },
                new ThicknessTransition { Property = Border.PaddingProperty, Duration = MoveTime, Easing = Ease },
            ];
            var size = target.Bounds.Width;
            target.Opacity = 0; // the moving logo stands in for it
            Canvas.SetLeft(Logo, at.X);
            Canvas.SetTop(Logo, at.Y);
            Logo.Width = size;
            Logo.Height = size;
            Logo.CornerRadius = target is Border b ? b.CornerRadius : new CornerRadius(size * 0.3);
            Logo.Padding = new Thickness(size / 4);
            Backdrop.Opacity = 0;
            Texts.Opacity = 0;
            await Task.Delay(MoveTime + TimeSpan.FromMilliseconds(50));
            target.Opacity = 1;
            return;
        }

        Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(350), Easing = Ease }];
        Opacity = 0;
        await Task.Delay(TimeSpan.FromMilliseconds(380));
    }

    /// <summary>Two beats, then a rest: like the design's 1.15 s loop.</summary>
    private static Animation Heartbeat()
    {
        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(1150),
            IterationCount = IterationCount.Infinite,
            Easing = new SineEaseInOut(),
        };
        foreach (var (cue, scale) in new[] { (0d, 1d), (0.14, 1.07), (0.28, 1d), (0.42, 1.045), (0.56, 1d), (1d, 1d) })
        {
            var frame = new KeyFrame { Cue = new Cue(cue) };
            frame.Setters.Add(new Setter(ScaleTransform.ScaleXProperty, scale));
            frame.Setters.Add(new Setter(ScaleTransform.ScaleYProperty, scale));
            animation.Children.Add(frame);
        }
        return animation;
    }

    private static Animation Fill(TimeSpan duration)
    {
        var from = new KeyFrame { Cue = new Cue(0) };
        var to = new KeyFrame { Cue = new Cue(1) };
        from.Setters.Add(new Setter(WidthProperty, 0d));
        to.Setters.Add(new Setter(WidthProperty, 128d));
        return new Animation { Duration = duration, FillMode = FillMode.Forward, Children = { from, to } };
    }
}
