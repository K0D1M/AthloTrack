using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace AthloTrack.Views.Controls;

/// <summary>
/// A workout's text in a list: the first few lines (fading out at the cut), and «Εμφάνιση ολόκληρου»
/// when there is more, which shows the whole workout in a window over the app («Άκυρο» closes it).
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

    // The one window open (a list can't open two): closed by Android's back via CloseOpen.
    private static Action? s_closeOpen;

    private static readonly IBrush Fade = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Colors.Black, 0), new GradientStop(Colors.Black, 0.55), new GradientStop(Colors.Transparent, 1) },
    };

    private readonly WorkoutText _text = new();
    private readonly Border _clip;
    private readonly Button _more;
    private Control? _window;

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

    /// <summary>Closes the open window, if any: Android's back does this first.</summary>
    public static bool CloseOpen()
    {
        if (s_closeOpen is not { } close) return false;
        close();
        return true;
    }

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
        Close(); // the page went away under it
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>The whole workout in a window over the app.</summary>
    private void Open()
    {
        if (_window is not null || OverlayLayer.GetOverlayLayer(this) is not { } layer) return;
        s_closeOpen?.Invoke();

        var title = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(Title) ? "Ασκησιολόγιο" : Title,
            FontSize = 17,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
        };
        title.Bind(TextBlock.ForegroundProperty, title.GetResourceObservable("HeadingBrush"));

        var scroll = new ScrollViewer
        {
            Content = new WorkoutText { Text = Text },
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        Grid.SetRow(scroll, 1);

        var cancel = new Button { Content = "Άκυρο", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        cancel.Classes.Add("admin-dialog");
        cancel.Click += (_, _) => Close();
        Grid.SetRow(cancel, 2);

        var card = new Border
        {
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(20, 18, 20, 16),
            Margin = new Thickness(16, 24),
            MaxWidth = 560,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Children = { title, scroll, cancel } },
        };
        card.Bind(Border.BackgroundProperty, card.GetResourceObservable("CardBrush"));

        // A tap outside the card closes it too; Esc on desktop.
        var root = new Grid { Background = new SolidColorBrush(Color.FromArgb(0x73, 0, 0, 0)), Focusable = true, Children = { card } };
        root.Tapped += (_, e) =>
        {
            if (e.Source == root) Close();
        };
        root.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            Close();
            e.Handled = true;
        };
        // The layer sizes nothing for us: cover all of it, and follow a rotation or a resized window.
        void Fit()
        {
            root.Width = layer.Bounds.Width;
            root.Height = layer.Bounds.Height;
            card.MaxHeight = Math.Max(240, layer.Bounds.Height - 48);
        }
        void OnLayerChanged(object? s, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == BoundsProperty) Fit();
        }
        Fit();
        layer.PropertyChanged += OnLayerChanged;
        root.DetachedFromVisualTree += (_, _) => layer.PropertyChanged -= OnLayerChanged;

        _window = root;
        s_closeOpen = Close;
        layer.Children.Add(root);
        root.Focus();
        Motion.FadeInPage(root);
    }

    private void Close()
    {
        if (_window is not { } window) return;
        _window = null;
        if (s_closeOpen == Close) s_closeOpen = null;
        (window.Parent as Panel)?.Children.Remove(window);
    }
}
