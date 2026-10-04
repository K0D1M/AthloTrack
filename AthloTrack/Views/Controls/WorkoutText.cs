using AthloTrack.Core.Workouts;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace AthloTrack.Views.Controls;

/// <summary>
/// A workout's text with its markup shown formatted (see <see cref="WorkoutMarkup"/>): headings,
/// bullet and numbered items with a hanging indent, and bold. Plain lines look as typed.
/// </summary>
public class WorkoutText : StackPanel
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<WorkoutText, string?>(nameof(Text));

    private static readonly IBrush HeadingBrush = new SolidColorBrush(Color.Parse("#003366"));

    public WorkoutText()
    {
        Spacing = 2;
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty) Build();
    }

    private void Build()
    {
        Children.Clear();
        var first = true;
        foreach (var line in WorkoutMarkup.Parse(Text))
        {
            switch (line.Kind)
            {
                case MarkupKind.Blank:
                    Children.Add(new Border { Height = 6 });
                    break;
                case MarkupKind.Heading:
                    var heading = Paragraph(line);
                    heading.FontWeight = FontWeight.SemiBold;
                    heading.FontSize = 15;
                    heading.Foreground = HeadingBrush;
                    if (!first) heading.Margin = new Thickness(0, 6, 0, 0);
                    Children.Add(heading);
                    break;
                case MarkupKind.Bullet:
                case MarkupKind.Numbered:
                    var marker = new TextBlock
                    {
                        Text = line.Kind == MarkupKind.Bullet ? "•" : $"{line.Number}.",
                        MinWidth = 18,
                        Margin = new Thickness(4, 0, 4, 0),
                        VerticalAlignment = VerticalAlignment.Top,
                    };
                    var content = Paragraph(line);
                    Grid.SetColumn(content, 1);
                    Children.Add(new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                        Children = { marker, content },
                    });
                    break;
                default:
                    Children.Add(Paragraph(line));
                    break;
            }

            first = false;
        }
    }

    private static TextBlock Paragraph(MarkupLine line)
    {
        var block = new TextBlock { TextWrapping = TextWrapping.Wrap };
        foreach (var span in line.Spans)
        {
            var run = new Run(span.Text);
            if (span.Bold) run.FontWeight = FontWeight.Bold;
            block.Inlines!.Add(run);
        }

        return block;
    }
}
