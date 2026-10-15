using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace AthloTrack.Views.Controls;

/// <summary>
/// 24-hour TimePicker whose empty-state placeholders read "ώρα / λεπτό". Avalonia hard-codes
/// "hour / minute" into the control, so they're overwritten after the control sets them
/// (like <see cref="GreekDatePicker"/>).
/// </summary>
public class GreekTimePicker : TimePicker
{
    private TextBlock? _hour, _minute;

    // Keep the stock TimePicker look.
    protected override Type StyleKeyOverride => typeof(TimePicker);

    public GreekTimePicker()
    {
        ClockIdentifier = "24HourClock";
        MinuteIncrement = 5;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _hour = e.NameScope.Find<TextBlock>("PART_HourTextBlock");
        _minute = e.NameScope.Find<TextBlock>("PART_MinuteTextBlock");
        ApplyGreekPlaceholders();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedTimeProperty) ApplyGreekPlaceholders();
    }

    private void ApplyGreekPlaceholders()
    {
        if (SelectedTime is not null) return;
        if (_hour is not null) _hour.Text = "ώρα";
        if (_minute is not null) _minute.Text = "λεπτό";
    }
}
