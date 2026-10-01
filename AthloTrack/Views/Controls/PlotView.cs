using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace AthloTrack.Views.Controls;

/// <summary>
/// Shows a ScottPlot chart as an image rendered at the control's pixel size. ScottPlot's own
/// Avalonia control froze the browser's single UI thread; plain image rendering works on every head.
/// </summary>
public sealed class PlotView : Control
{
    private ScottPlot.Plot? _plot;
    private Bitmap? _bitmap;
    private PixelSize _renderedSize;

    public ScottPlot.Plot? Plot
    {
        get => _plot;
        set
        {
            _plot = value;
            _bitmap?.Dispose();
            _bitmap = null;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_plot is null || Bounds.Width < 1 || Bounds.Height < 1) return;

        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var size = new PixelSize((int)Math.Ceiling(Bounds.Width * scale), (int)Math.Ceiling(Bounds.Height * scale));
        if (_bitmap is null || size != _renderedSize)
        {
            try
            {
                // Same physical text/line size on any screen density.
                _plot.ScaleFactor = (float)scale;
                var png = _plot.GetImageBytes(size.Width, size.Height, ScottPlot.ImageFormat.Png);
                _bitmap?.Dispose();
                _bitmap = new Bitmap(new MemoryStream(png));
                _renderedSize = size;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AthloTrack] Chart render failed: {ex.Message}");
                return;
            }
        }
        context.DrawImage(_bitmap, new Rect(Bounds.Size));
    }
}
