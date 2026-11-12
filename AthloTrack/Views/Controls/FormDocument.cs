using System;
using System.Collections.Generic;
using System.Globalization;
using AthloTrack.Core.Forms;
using AthloTrack.Core.Workouts;
using AthloTrack.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace AthloTrack.Views.Controls;

/// <summary>How a form's document is shown: filled in by the athlete, read, or arranged by the coach.</summary>
public enum FormDocumentMode { Read, Fill, Design }

/// <summary>
/// A form's text with its fields in place (see <see cref="FormFields"/>), formatted like
/// <see cref="WorkoutText"/>. Fill: boxes, tick boxes and dates to fill in. Read: the answers
/// underlined in the text. Design: each field a chip the coach taps (<see cref="FieldTapped"/>).
/// </summary>
public class FormDocument : StackPanel
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<FormDocument, string?>(nameof(Text));

    public static readonly StyledProperty<FormFieldValues?> ValuesProperty =
        AvaloniaProperty.Register<FormDocument, FormFieldValues?>(nameof(Values));

    public static readonly StyledProperty<FormDocumentMode> ModeProperty =
        AvaloniaProperty.Register<FormDocument, FormDocumentMode>(nameof(Mode));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public FormFieldValues? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public FormDocumentMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    /// <summary>Design: a field's chip was tapped (its number, and the chip to anchor a menu on).</summary>
    public event Action<FormField, Control>? FieldTapped;

    private readonly Dictionary<string, Control> _inputs = new();

    public FormDocument()
    {
        Spacing = 8;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValuesProperty)
        {
            if (change.OldValue is FormFieldValues old) old.MissingChanged -= ShowMissing;
            if (change.NewValue is FormFieldValues now) now.MissingChanged += ShowMissing;
        }
        if (change.Property == TextProperty || change.Property == ValuesProperty || change.Property == ModeProperty) Build();
    }

    private void Build()
    {
        Children.Clear();
        _inputs.Clear();
        var next = 0;
        foreach (var line in WorkoutMarkup.Parse(Text))
        {
            if (line.Kind == MarkupKind.Blank)
            {
                Children.Add(new Border { Height = 2 });
                continue;
            }

            var heading = line.Kind == MarkupKind.Heading;
            var flow = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var below = new List<Control>(); // «Πολλές γραμμές»: a full-width box under the line
            foreach (var span in line.Spans)
            {
                foreach (var piece in FormFields.Split(span.Text, ref next))
                {
                    if (piece.Field is { } field)
                    {
                        if (field.Kind == FormFieldKind.Long && Mode != FormDocumentMode.Design) below.Add(FieldControl(field));
                        else flow.Children.Add(FieldControl(field));
                        continue;
                    }
                    foreach (var word in Words(piece.Text))
                        flow.Children.Add(Word(word, span.Bold || heading, heading));
                }
            }

            Control row = flow;
            if (line.Kind is MarkupKind.Bullet or MarkupKind.Numbered)
            {
                var marker = new TextBlock
                {
                    Text = line.Kind == MarkupKind.Bullet ? "•" : $"{line.Number}.",
                    MinWidth = 18,
                    Margin = new Thickness(4, 6, 4, 0),
                    VerticalAlignment = VerticalAlignment.Top,
                };
                Grid.SetColumn(flow, 1);
                row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { marker, flow } };
            }
            if (heading && Children.Count > 0) row.Margin = new Thickness(0, 6, 0, 0);
            Children.Add(row);
            foreach (var b in below) Children.Add(b);
        }
        ShowMissing();
    }

    /// <summary>Words with their trailing space, so the line wraps between them.</summary>
    private static IEnumerable<string> Words(string text)
    {
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != ' ') continue;
            yield return text[start..(i + 1)];
            start = i + 1;
        }
        if (start < text.Length) yield return text[start..];
    }

    private TextBlock Word(string word, bool bold, bool heading)
    {
        var t = new TextBlock
        {
            Text = word,
            FontWeight = bold ? (heading ? FontWeight.SemiBold : FontWeight.Bold) : FontWeight.Normal,
            FontSize = heading ? 15 : 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 6),
        };
        if (heading) t.Bind(TextBlock.ForegroundProperty, t.GetResourceObservable("HeadingBrush"));
        return t;
    }

    private Control FieldControl(FormField field) => Mode switch
    {
        FormDocumentMode.Fill => FillControl(field),
        FormDocumentMode.Design => Chip(field),
        _ => ReadControl(field),
    };

    // ---------------- Fill: the athlete's inputs ----------------

    private Control FillControl(FormField f)
    {
        var values = Values;
        var value = values?.Get(f.Key);
        Control input;
        switch (f.Kind)
        {
            case FormFieldKind.Tick:
                var box = new CheckBox { IsChecked = value == FormFields.Ticked, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
                box.IsCheckedChanged += (_, _) => values?.Set(f.Key, box.IsChecked == true ? FormFields.Ticked : null);
                AutomationName(box, "Κουτάκι");
                input = box;
                break;
            case FormFieldKind.Date:
                var picker = new GreekDatePicker { Margin = new Thickness(0, 2, 6, 2), VerticalAlignment = VerticalAlignment.Center };
                if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    picker.SelectedDate = new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue));
                picker.SelectedDateChanged += (_, _) =>
                    values?.Set(f.Key, picker.SelectedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                input = picker;
                break;
            case FormFieldKind.Long:
                var area = new TextBox { Text = value, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 88, Watermark = "γράψε εδώ" };
                area.Classes.Add("blank");
                area.TextChanged += (_, _) => values?.Set(f.Key, area.Text);
                input = area;
                break;
            default:
                var text = new TextBox { Text = value, MinWidth = 170, Watermark = "γράψε εδώ", Margin = new Thickness(0, 2, 6, 2), VerticalAlignment = VerticalAlignment.Center };
                text.Classes.Add("blank");
                text.TextChanged += (_, _) => values?.Set(f.Key, text.Text);
                input = text;
                break;
        }
        _inputs[f.Key] = input;
        return input;
    }

    private static void AutomationName(Control c, string name) =>
        Avalonia.Automation.AutomationProperties.SetName(c, name);

    private void ShowMissing()
    {
        if (Values is not { } values) return;
        foreach (var (key, input) in _inputs) input.Classes.Set("missing", values.IsMissing(key));
    }

    // ---------------- Read: the answers in place ----------------

    private Control ReadControl(FormField f)
    {
        var value = Values?.Get(f.Key);
        if (f.Kind == FormFieldKind.Tick)
        {
            var ticked = value == FormFields.Ticked;
            var square = new Border
            {
                Width = 16,
                Height = 16,
                BorderThickness = new Thickness(1.4),
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = ticked ? new Path { Data = Geometry.Parse("M3,3 L11,11 M11,3 L3,11"), StrokeThickness = 1.8, StrokeLineCap = PenLineCap.Round } : null,
            };
            square.Bind(Border.BorderBrushProperty, square.GetResourceObservable("BrandSupportBrush"));
            if (square.Child is Path p) p.Bind(Shape.StrokeProperty, p.GetResourceObservable("HeadingBrush"));
            return square;
        }

        var shown = FormFields.Display(f, value);
        var answer = new TextBlock
        {
            Text = shown.Length == 0 ? " " : shown,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            MinWidth = shown.Length == 0 ? 90 : 0,
        };
        answer.Bind(TextBlock.ForegroundProperty, answer.GetResourceObservable("HeadingBrush"));
        var line = new Border
        {
            Child = answer,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(4, 0, 4, 1),
            Margin = new Thickness(0, 4, 6, 4),
            VerticalAlignment = VerticalAlignment.Center,
        };
        line.Bind(Border.BorderBrushProperty, line.GetResourceObservable("MutedTextBrush"));
        if (f.Kind == FormFieldKind.Long) line.HorizontalAlignment = HorizontalAlignment.Stretch;
        return line;
    }

    // ---------------- Design: the coach's chips ----------------

    private static readonly Dictionary<FormFieldKind, string> Icons = new()
    {
        [FormFieldKind.Text] = "M4,7V5H20V7M9,19H15M12,5V19",
        [FormFieldKind.Long] = "M4,6H20M4,10H20M4,14H20M4,18H14",
        [FormFieldKind.Date] = "M7,3V6M17,3V6M4,8H20M5,5H19A1,1 0 0 1 20,6V19A1,1 0 0 1 19,20H5A1,1 0 0 1 4,19V6A1,1 0 0 1 5,5Z",
        [FormFieldKind.Tick] = "M5,4H19A1,1 0 0 1 20,5V19A1,1 0 0 1 19,20H5A1,1 0 0 1 4,19V5A1,1 0 0 1 5,4ZM8,12L11,15L16,9",
    };

    private Control Chip(FormField f)
    {
        var icon = new Path { Data = Geometry.Parse(Icons[f.Kind]), StrokeThickness = 2, Width = 14, Height = 14, Stretch = Stretch.Uniform, StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round };
        var label = new TextBlock { Text = FormFields.Name(f.Kind) + (f.Required ? " *" : ""), FontSize = 13, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        var chip = new Button
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { icon, label } },
            Margin = new Thickness(0, 3, 6, 3),
            VerticalAlignment = VerticalAlignment.Center,
        };
        chip.Classes.Add("fieldchip");
        icon.Bind(Shape.StrokeProperty, chip.GetObservable(TemplatedControl.ForegroundProperty));
        AutomationName(chip, $"Πεδίο {f.Index + 1}: {FormFields.Name(f.Kind)}");
        chip.Click += (_, _) => FieldTapped?.Invoke(f, chip);
        return chip;
    }
}
