using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace AthloTrack.Views.Controls;

/// <summary>
/// DatePicker whose empty-state placeholders read "ημέρα / μήνας / έτος". Avalonia hard-codes
/// "day / month / year" into the control, so they're overwritten after the control sets them.
/// </summary>
public class GreekDatePicker : DatePicker
{
    private TextBlock? _day, _month, _year;

    // Keep the stock DatePicker look.
    protected override Type StyleKeyOverride => typeof(DatePicker);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _day = e.NameScope.Find<TextBlock>("PART_DayTextBlock");
        _month = e.NameScope.Find<TextBlock>("PART_MonthTextBlock");
        _year = e.NameScope.Find<TextBlock>("PART_YearTextBlock");
        ApplyGreekPlaceholders();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedDateProperty) ApplyGreekPlaceholders();
    }

    private void ApplyGreekPlaceholders()
    {
        if (SelectedDate is not null) return;
        if (_day is not null) _day.Text = "ημέρα";
        if (_month is not null) _month.Text = "μήνας";
        if (_year is not null) _year.Text = "έτος";
    }
}
