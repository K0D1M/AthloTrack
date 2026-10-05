using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace AthloTrack.Views.Controls;

/// <summary>
/// A small turning arc in the current text colour, e.g. inside a button while it saves.
/// The turning is the style animation in Styles/Motion.axaml.
/// </summary>
public class Spinner : Control
{
    static Spinner()
    {
        AffectsRender<Spinner>(TextElement.ForegroundProperty);
    }

    public Spinner()
    {
        Width = 16;
        Height = 16;
        RenderTransformOrigin = RelativePoint.Center;
    }

    public override void Render(DrawingContext context)
    {
        var brush = GetValue(TextElement.ForegroundProperty) ?? Brushes.White;
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0) return;

        const double thickness = 2;
        var radius = (size - thickness) / 2;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        // Three quarters of a circle: from the top, clockwise to the left.
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(new Point(center.X, center.Y - radius), false);
            g.ArcTo(new Point(center.X - radius, center.Y), new Size(radius, radius), 0, true, SweepDirection.Clockwise);
            g.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(brush, thickness, lineCap: PenLineCap.Round), geometry);
    }
}
