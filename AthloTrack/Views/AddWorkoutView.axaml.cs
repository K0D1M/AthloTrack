using System;
using System.Linq;
using AthloTrack.Core.Workouts;
using AthloTrack.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AthloTrack.Views;

public partial class AddWorkoutView : UserControl
{
    private AddWorkoutViewModel? _vm;
    private string _lastText = string.Empty;
    private bool _applying;
    private IInputPane? _inputPane;
    private double _keyboardInset;

    public AddWorkoutView()
    {
        InitializeComponent();
        Editor.TextChanged += OnEditorTextChanged;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null) _vm.InsertRequested -= InsertLine;
        _vm = DataContext as AddWorkoutViewModel;
        if (_vm is not null) _vm.InsertRequested += InsertLine;
        _lastText = Editor.Text ?? string.Empty;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _inputPane = TopLevel.GetTopLevel(this)?.InputPane;
        if (_inputPane is not null) _inputPane.StateChanged += OnInputPaneChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_vm is not null) _vm.InsertRequested -= InsertLine;
        if (_inputPane is not null) _inputPane.StateChanged -= OnInputPaneChanged;
        _inputPane = null;
    }

    /// <summary>
    /// Phone keyboard: the app's view isn't resized for it, so the form would go on behind it.
    /// While it's open the form ends above it, and the line being typed is scrolled into view.
    /// </summary>
    private void OnInputPaneChanged(object? sender, InputPaneStateEventArgs e)
    {
        var inset = 0.0;
        if (e.NewState == InputPaneState.Open && TopLevel.GetTopLevel(this) is { } top
            && this.TranslatePoint(new Point(0, Bounds.Height), top) is { } bottom)
        {
            inset = Math.Max(0, bottom.Y - e.EndRect.Top);
        }

        _keyboardInset = inset;
        Form.Margin = new Thickness(16, 16, 16, 16 + inset);
        if (inset > 0) Dispatcher.UIThread.Post(BringCaretIntoView, DispatcherPriority.Background);
    }

    private void BringCaretIntoView()
    {
        if (_keyboardInset <= 0 || !Editor.IsFocused) return;
        var presenter = Editor.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        if (presenter is null) return;
        var caret = presenter.TextLayout.HitTestTextPosition(Math.Clamp(Editor.CaretIndex, 0, (Editor.Text ?? string.Empty).Length));
        // A little room below the line, so it doesn't sit right on the keyboard's edge.
        presenter.BringIntoView(new Rect(caret.X, caret.Y, Math.Max(caret.Width, 1), caret.Height + 24));
    }

    /// <summary>Toolbar: formatting (Tag bold/bullet/numbered/heading) or a symbol to insert.</summary>
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
            "heading" => WorkoutMarkup.ToggleLinePrefix(text, start, end, MarkupKind.Heading),
            _ => Replace(text, start, end, tag),
        };
        Apply(edit);
        Editor.Focus();
    }

    /// <summary>A new line in a list continues it (works for phone keyboards too: it reacts to the text).</summary>
    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_applying) return;
        var old = _lastText;
        var now = Editor.Text ?? string.Empty;
        _lastText = now;
        if (_keyboardInset > 0) Dispatcher.UIThread.Post(BringCaretIntoView, DispatcherPriority.Background);

        // After the TextBox has moved its caret past the typed character.
        Dispatcher.UIThread.Post(() =>
        {
            if ((Editor.Text ?? string.Empty) != now) return;
            if (WorkoutMarkup.ContinueList(old, now, Editor.CaretIndex) is { } edit) Apply(edit);
        });
    }

    /// <summary>The exercise builder's line, on its own line after the caret's line.</summary>
    private void InsertLine(string line)
    {
        var text = Editor.Text ?? string.Empty;
        var caret = Math.Clamp(Editor.CaretIndex, 0, text.Length);
        var nextBreak = text.IndexOf('\n', caret);
        var lineEnd = nextBreak < 0 ? text.Length : nextBreak;
        if (lineEnd > 0 && text[lineEnd - 1] == '\r') lineEnd--;
        var lineStart = caret == 0 ? 0 : text.LastIndexOf('\n', caret - 1) + 1;
        var currentEmpty = string.IsNullOrWhiteSpace(text[lineStart..lineEnd]);

        string result;
        int newCaret;
        if (currentEmpty)
        {
            result = text[..lineStart] + line + text[lineEnd..];
            newCaret = lineStart + line.Length;
        }
        else
        {
            result = text[..lineEnd] + "\n" + line + text[lineEnd..];
            newCaret = lineEnd + 1 + line.Length;
        }

        if (_vm is not null) _vm.IsPreview = false;
        Apply(new TextEdit(result, newCaret, newCaret));
    }

    private static TextEdit Replace(string text, int start, int end, string value)
    {
        var (s, e) = start <= end ? (start, end) : (end, start);
        s = Math.Clamp(s, 0, text.Length);
        e = Math.Clamp(e, 0, text.Length);
        var result = text.Remove(s, e - s).Insert(s, value);
        return new TextEdit(result, s + value.Length, s + value.Length);
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
    }
}
