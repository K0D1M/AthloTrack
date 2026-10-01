using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace AthloTrack.Views.Controls;

/// <summary>
/// Runs <c>Command(CommandParameter)</c> once the control has been on screen (at least half of it,
/// or 150 px of a tall one) for a second: a workout counts as read only when the athlete saw it.
/// </summary>
public static class SeenTracker
{
    private static readonly TimeSpan Dwell = TimeSpan.FromSeconds(1);

    public static readonly AttachedProperty<ICommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>("Command", typeof(SeenTracker));

    public static readonly AttachedProperty<object?> CommandParameterProperty =
        AvaloniaProperty.RegisterAttached<Control, object?>("CommandParameter", typeof(SeenTracker));

    private static readonly AttachedProperty<State?> StateProperty =
        AvaloniaProperty.RegisterAttached<Control, State?>("State", typeof(SeenTracker));

    public static ICommand? GetCommand(Control c) => c.GetValue(CommandProperty);
    public static void SetCommand(Control c, ICommand? value) => c.SetValue(CommandProperty, value);
    public static object? GetCommandParameter(Control c) => c.GetValue(CommandParameterProperty);
    public static void SetCommandParameter(Control c, object? value) => c.SetValue(CommandParameterProperty, value);

    static SeenTracker()
    {
        CommandProperty.Changed.AddClassHandler<Control>((control, e) =>
        {
            if (e.NewValue is null || control.GetValue(StateProperty) is not null) return;
            var state = new State(control);
            control.SetValue(StateProperty, state);
            control.EffectiveViewportChanged += state.OnViewportChanged;
        });
    }

    private sealed class State(Control control)
    {
        private IDisposable? _pending;
        private bool _done;

        public void OnViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
        {
            if (_done) return;
            var visible = e.EffectiveViewport.Intersect(new Rect(control.Bounds.Size));
            var needed = Math.Min(control.Bounds.Height * 0.5, 150);
            if (control.Bounds.Height > 0 && visible.Height >= needed && visible.Width > 0)
            {
                // RunOnce: a started DispatcherTimer instance never ticked here (Android, Avalonia 12).
                _pending ??= DispatcherTimer.RunOnce(OnDwellElapsed, Dwell);
            }
            else
            {
                _pending?.Dispose();
                _pending = null;
            }
        }

        private void OnDwellElapsed()
        {
            _pending = null;
            if (_done || !control.IsEffectivelyVisible) return;
            _done = true;
            control.EffectiveViewportChanged -= OnViewportChanged;
            var command = GetCommand(control);
            var parameter = GetCommandParameter(control);
            if (command?.CanExecute(parameter) == true) command.Execute(parameter);
        }
    }
}
