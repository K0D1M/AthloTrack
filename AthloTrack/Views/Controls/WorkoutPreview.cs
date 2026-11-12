using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AthloTrack.Views.Controls;

/// <summary>
/// A workout's text in a list: the first few lines (fading out at the cut), and «Εμφάνιση ολόκληρου»
/// when there is more, which shows the whole workout in a window over the app («Κλείσιμο» closes it).
/// Short workouts show whole, with no button.
/// </summary>
public class WorkoutPreview : StackPanel
{
    /// <summary>About five lines of text.</summary>
    public const double CollapsedHeight = 108;

    /// <summary>Hiding less than about a line isn't worth a button: the text shows whole.</summary>
    private const double Slack = 24;

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<WorkoutPreview, string?>(nameof(Text));

    /// <summary>The window's title («Ασκησιολόγιο · Τετ 12/11»).</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<WorkoutPreview, string?>(nameof(Title));

    private static readonly IBrush Fade = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Colors.Black, 0), new GradientStop(Colors.Black, 0.55), new GradientStop(Colors.Transparent, 1) },
    };

    private readonly WorkoutText _text = new();
    private readonly Border _clip;
    private readonly Button _more;
    private Action? _close;

    public WorkoutPreview()
    {
        Spacing = 2;
        _clip = new Border { Child = _text, ClipToBounds = true };
        _more = new Button { Content = "Εμφάνιση ολόκληρου", IsVisible = false };
        _more.Classes.Add("showmore");
        _more.Click += (_, _) => Open();
        Children.Add(_clip);
        Children.Add(_more);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Whether the text is cut (the button shows).</summary>
    public bool IsCut => _more.IsVisible;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            _text.Text = Text;
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // The whole text's height at this width decides whether to cut it.
        _text.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        var cut = _text.DesiredSize.Height > CollapsedHeight + Slack;
        _clip.MaxHeight = cut ? CollapsedHeight : double.PositiveInfinity;
        _clip.OpacityMask = cut ? Fade : null;
        _more.IsVisible = cut;
        return base.MeasureOverride(availableSize);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _close?.Invoke(); // the page went away under it
        _close = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>The whole workout in a window over the app (OverlayDialog).</summary>
    private void Open() =>
        _close = OverlayDialog.Show(this, string.IsNullOrWhiteSpace(Title) ? "Ασκησιολόγιο" : Title,
            new WorkoutText { Text = Text });
}
