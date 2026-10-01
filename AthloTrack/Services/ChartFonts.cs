using System;
using System.IO;
using Avalonia.Platform;
using SkiaSharp;

namespace AthloTrack.Services;

/// <summary>
/// Gives the progress chart (ScottPlot) the bundled Inter typeface, which has Greek. The browser
/// head has no system fonts, so chart labels and the legend would otherwise not render.
/// </summary>
public sealed class ChartFonts : ScottPlot.IFontResolver
{
    public const string Family = "Inter";

    private readonly SKTypeface? _regular = Load("Inter-Regular.ttf");
    private readonly SKTypeface? _bold = Load("Inter-SemiBold.ttf");

    /// <summary>Registers the resolver once, at startup.</summary>
    public static void Register()
    {
        try
        {
            ScottPlot.Fonts.FontResolvers.Insert(0, new ChartFonts());
            ScottPlot.Fonts.Default = Family;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AthloTrack] Chart font setup failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public SKTypeface? CreateTypeface(string fontName, bool bold, bool italic) =>
        string.Equals(fontName, Family, StringComparison.OrdinalIgnoreCase) ? (bold ? _bold : _regular) : null;

    public SKTypeface? CreateTypeface(string fontName, ScottPlot.FontWeight weight, ScottPlot.FontSlant slant, ScottPlot.FontSpacing spacing) =>
        CreateTypeface(fontName, weight >= ScottPlot.FontWeight.SemiBold, slant != ScottPlot.FontSlant.Upright);

    private static SKTypeface? Load(string file)
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri($"avares://Avalonia.Fonts.Inter/Assets/{file}"));
            // SKTypeface needs a seekable stream it can keep.
            var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            buffer.Position = 0;
            return SKTypeface.FromStream(buffer);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AthloTrack] Chart font {file} failed to load: {ex.Message}");
            return null;
        }
    }
}
