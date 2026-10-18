using System;
using AthloTrack.Core.Workouts;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace AthloTrack.Views.Controls;

/// <summary>
/// Free text with the Ασκησιολόγιο markup: the same toolbar (bold, bullets, numbering, heading),
/// lists that continue on a new line, and a formatted preview. The caret handling is the workout
/// editor's (AddWorkoutView), kept separate so that editor stays as it is.
/// </summary>
public partial class MarkupEditor : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MarkupEditor, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private string _lastText = string.Empty;
    private bool _applying;
    private bool _syncing;

    public MarkupEditor()
    {
        InitializeComponent();
        Editor.TextChanged += OnEditorTextChanged;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != TextProperty || _syncing) return;
        var value = Text ?? string.Empty;
        if ((Editor.Text ?? string.Empty) == value) return;
        _applying = true;
        try
        {
            Editor.Text = value;
            _lastText = value;
        }
        finally
        {
            _applying = false;
        }
        PreviewText.Text = value;
    }

    private void PushText(string text)
    {
        _syncing = true;
        try
        {
            SetCurrentValue(TextProperty, text);
        }
        finally
        {
            _syncing = false;
        }
        PreviewText.Text = text;
    }

    private void OnPreviewChanged(object? sender, RoutedEventArgs e)
    {
        var preview = PreviewToggle.IsChecked == true;
        PreviewText.Text = Editor.Text ?? string.Empty;
        Preview.IsVisible = preview;
        Editor.IsVisible = !preview;
        Toolbar.IsVisible = !preview;
    }

    /// <summary>Toolbar: Tag bold / bullet / numbered / heading.</summary>
    private void OnFormat(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is not string tag) return;
        var text = Editor.Text ?? string.Empty;
        var start = Editor.SelectionStart;
        var end = Editor.SelectionEnd;

        var edit = tag switch
        {
            "bold" => WorkoutMarkup.ToggleBold(text, start, end),
            "bullet" => WorkoutMarkup.ToggleLinePrefix(text, start, end, MarkupKind.Bullet),
            "numbered" => WorkoutMarkup.ToggleLinePrefix(text, start, end, MarkupKind.Numbered),
            _ => WorkoutMarkup.ToggleLinePrefix(text, start, end, MarkupKind.Heading),
        };
        Apply(edit);
        Editor.Focus();
    }

    /// <summary>A new line in a list continues it (phone keyboards too: it reacts to the text).</summary>
    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_applying) return;
        var old = _lastText;
        var now = Editor.Text ?? string.Empty;
        _lastText = now;
        PushText(now);

        Dispatcher.UIThread.Post(() =>
        {
            if ((Editor.Text ?? string.Empty) != now) return;
            if (WorkoutMarkup.ContinueList(old, now, Editor.CaretIndex) is { } edit) Apply(edit);
        });
    }

    private void Apply(TextEdit edit)
    {
        _applying = true;
        try
        {
            Editor.Text = edit.Text;
            _lastText = edit.Text;
            // Caret first: setting it afterwards would collapse the selection.
            Editor.CaretIndex = edit.SelectionEnd;
            Editor.SelectionStart = edit.SelectionStart;
            Editor.SelectionEnd = edit.SelectionEnd;
        }
        finally
        {
            _applying = false;
        }
        PushText(edit.Text);
    }
}
