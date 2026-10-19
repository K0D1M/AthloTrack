using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;

namespace AthloTrack.Views;

/// <summary>
/// Whether (and how tall) the phone's on-screen keyboard is open. The app itself ends above the
/// keyboard (App.OnInputPaneChanged); views use this only for extras, such as hiding the login
/// motto or keeping the typed line in view.
/// </summary>
public sealed class KeyboardInset
{
    private readonly Control _view;
    private readonly Action<double> _changed;
    private IInputPane? _pane;

    /// <param name="changed">Called with the keyboard's height (0 when it closes).</param>
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
        if (e.NewState == InputPaneState.Open && TopLevel.GetTopLevel(_view) is { } top)
        {
            inset = Math.Max(0, top.ClientSize.Height - e.EndRect.Top);
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
