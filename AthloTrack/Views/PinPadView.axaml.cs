using System;
using System.ComponentModel;
using AthloTrack.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace AthloTrack.Views;

public partial class PinPadView : UserControl
{
    private PinPadViewModel? _vm;

    public PinPadView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
            _vm = DataContext as PinPadViewModel;
            if (_vm is not null) _vm.PropertyChanged += OnVmChanged;
        };
    }

    // Desktop/web: type the PIN with the keyboard.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dispatcher.UIThread.Post(() => Focus());
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_vm is null) return;
        var digit = e.Key switch
        {
            >= Key.D0 and <= Key.D9 => (char)('0' + (e.Key - Key.D0)),
            >= Key.NumPad0 and <= Key.NumPad9 => (char)('0' + (e.Key - Key.NumPad0)),
            _ => (char?)null,
        };
        if (digit is { } d)
        {
            _vm.Digit(d.ToString());
            e.Handled = true;
        }
        else if (e.Key == Key.Back)
        {
            _vm.Backspace();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _vm.CanCancel)
        {
            _vm.CancelCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PinPadViewModel.ShakeCount)) Shake();
    }

    /// <summary>A wrong PIN: the dots shake sideways for a moment.</summary>
    private void Shake()
    {
        if (Dots.RenderTransform is not TranslateTransform move) return;
        double[] offsets = [-12, 10, -8, 6, -3, 0];
        var step = 0;
        DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(45) };
        timer.Tick += (_, _) =>
        {
            move.X = offsets[step++];
            if (step == offsets.Length) timer.Stop();
        };
        timer.Start();
    }
}
