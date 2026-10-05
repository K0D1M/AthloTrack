using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;

namespace AthloTrack.Views;

/// <summary>
/// How much of a view the phone's on-screen keyboard covers. The app's view isn't resized for
/// the keyboard, so views with text fields use this to keep their content above it.
/// </summary>
public sealed class KeyboardInset
{
    private readonly Control _view;
    private readonly Action<double> _changed;
    private IInputPane? _pane;

    /// <param name="changed">Called with the covered height (0 when the keyboard closes).</param>
    public KeyboardInset(Control view, Action<double> changed)
    {
        _view = view;
        _changed = changed;
        view.AttachedToVisualTree += (_, _) =>
        {
            _pane = TopLevel.GetTopLevel(_view)?.InputPane;
            if (_pane is not null) _pane.StateChanged += OnStateChanged;
        };
        view.DetachedFromVisualTree += (_, _) =>
        {
            if (_pane is not null) _pane.StateChanged -= OnStateChanged;
            _pane = null;
            Update(0);
        };
    }

    public double Value { get; private set; }

    private void OnStateChanged(object? sender, InputPaneStateEventArgs e)
    {
        var inset = 0.0;
        if (e.NewState == InputPaneState.Open && TopLevel.GetTopLevel(_view) is { } top
            && _view.TranslatePoint(new Point(0, _view.Bounds.Height), top) is { } bottom)
        {
            inset = Math.Max(0, bottom.Y - e.EndRect.Top);
        }

        Update(inset);
    }

    private void Update(double inset)
    {
        if (inset == Value) return;
        Value = inset;
        _changed(inset);
    }
}
