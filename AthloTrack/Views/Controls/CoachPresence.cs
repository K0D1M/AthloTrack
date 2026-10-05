using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace AthloTrack.Views.Controls;

/// <summary>
/// The coach's presence at a workout: green dot «Ο προπονητής θα είναι παρών», red dot «… δεν θα
/// είναι παρών», nothing at all when <see cref="Present"/> is null (not stated).
/// </summary>
public class CoachPresence : StackPanel
{
    public static readonly StyledProperty<bool?> PresentProperty =
        AvaloniaProperty.Register<CoachPresence, bool?>(nameof(Present));

    private readonly Ellipse _dot = new() { Width = 10, Height = 10, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _text = new()
    {
        FontSize = 12,
        FontWeight = FontWeight.SemiBold,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public CoachPresence()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 6;
        Children.Add(_dot);
        Children.Add(_text);
        Update();
    }

    public bool? Present
    {
        get => GetValue(PresentProperty);
        set => SetValue(PresentProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PresentProperty) Update();
    }

    private void Update()
    {
        IsVisible = Present is not null;
        if (Present is not { } present) return;

        // Theme tokens, so the green/red follow Light/Dark (Styles/Theme.axaml).
        var brush = this.GetResourceObservable(present ? "SuccessBrush" : "DangerBrush");
        _dot.Bind(Shape.FillProperty, brush);
        _text.Bind(TextBlock.ForegroundProperty, brush);
        _text.Text = present ? "Ο προπονητής θα είναι παρών" : "Ο προπονητής δεν θα είναι παρών";
    }
}
