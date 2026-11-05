using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AthloTrack.Core.Models;
using AthloTrack.Core.Workouts;
using SkiaSharp;

namespace AthloTrack.Services;

/// <summary>
/// A submitted «Έντυπο» as a formal A4 PDF: the AthloTrack logo and name, the form's title, the
/// athlete and submission date, the intro text, then each question with its answer; «Σελίδα x από y»
/// at the foot. The coach's name is the PDF's author.
/// Drawn with SkiaSharp's PDF backend (in the Windows, Android and WebAssembly builds alike),
/// with Inter embedded so Greek renders everywhere.
/// </summary>
public sealed class FormPdf
{
    private const float PageW = 595, PageH = 842; // A4 in points
    private const float Left = 56, Right = PageW - 56, Top = 48, Bottom = PageH - 64;
    private const float BodyTop = Top + 46;

    private static readonly SKColor Navy = SKColor.Parse("#003366");
    private static readonly SKColor Blue = SKColor.Parse("#1565C0");
    private static readonly SKColor Ink = SKColor.Parse("#1C2430");
    private static readonly SKColor Muted = SKColor.Parse("#5B6573");
    private static readonly SKColor Rule = SKColor.Parse("#D5DCE6");
    private static readonly SKColor AnswerFill = SKColor.Parse("#F3F6FA");

    private const string LogoPath =
        "M7.5,4A5.5,5.5 0 0,0 2,9.5C2,10 2.09,10.5 2.22,11H6.3L7.57,7.63C7.87,6.83 9.05,6.75 9.43,7.63L11.5,13L12.09,11.58C12.22,11.25 12.57,11 13,11H21.78C21.91,10.5 22,10 22,9.5A5.5,5.5 0 0,0 16.5,4C14.64,4 13,4.93 12,6.34C11,4.93 9.36,4 7.5,4M3,12.5A1,1 0 0,0 2,13.5A1,1 0 0,0 3,14.5H5.44L11,20C12,20.9 12,20.9 13,20L18.56,14.5H21A1,1 0 0,0 22,13.5A1,1 0 0,0 21,12.5H13.4L12.47,14.8C12.07,15.81 10.92,15.67 10.55,14.83L8.5,9.5L7.54,11.83C7.39,12.23 7.03,12.5 6.6,12.5H3Z";

    private readonly SKTypeface _regular;
    private readonly SKTypeface _bold;

    /// <param name="regular">Inter Regular (the app's font) unless a test gives another.</param>
    public FormPdf(SKTypeface regular, SKTypeface bold)
    {
        _regular = regular;
        _bold = bold;
    }

    /// <summary>With the Inter that ships inside the app (needs Avalonia running).</summary>
    public static FormPdf WithAppFonts()
    {
        static SKTypeface Load(string file)
        {
            using var s = Avalonia.Platform.AssetLoader.Open(new Uri($"avares://Avalonia.Fonts.Inter/Assets/{file}"));
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return SKTypeface.FromData(SKData.CreateCopy(ms.ToArray())) ?? SKTypeface.Default;
        }
        return new FormPdf(Load("Inter-Regular.ttf"), Load("Inter-Bold.ttf"));
    }

    /// <summary>«Τίτλος - Αθλητής.pdf», without characters files can't have.</summary>
    public static string FileName(FormDefinition form, string athleteName)
    {
        var name = $"{form.Title} - {athleteName}";
        foreach (var c in Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']))
            name = name.Replace(c, ' ');
        return name.Trim() + ".pdf";
    }

    public byte[] Create(FormDefinition form, FormAssignment assignment, string athleteName)
    {
        var blocks = Layout(form, assignment);
        // Pass 1 finds the page breaks, pass 2 draws with «από y» known.
        var pages = Paginate(blocks, FirstPageHeaderHeight(form));
        using var stream = new MemoryStream();
        using (var wstream = new SKManagedWStream(stream))
        using (var doc = SKDocument.CreatePdf(wstream, new SKDocumentPdfMetadata
        {
            Title = form.Title,
            Author = form.CoachName,
            Subject = athleteName,
            Creator = "AthloTrack",
            Producer = "AthloTrack",
            Creation = DateTime.Now,
        }))
        {
            for (var i = 0; i < pages.Count; i++)
            {
                var canvas = doc.BeginPage(PageW, PageH);
                Header(canvas, form, assignment, athleteName, first: i == 0);
                foreach (var (block, y) in pages[i]) block.Draw(canvas, y);
                Footer(canvas, form, i + 1, pages.Count);
                doc.EndPage();
            }
            doc.Close();
        }
        return stream.ToArray();
    }

    // ---------------- layout ----------------

    /// <summary>A piece of the body that is kept on one page.</summary>
    private sealed record Block(float Height, float SpaceBefore, Action<SKCanvas, float> Draw);

    private List<List<(Block Block, float Y)>> Paginate(List<Block> blocks, float firstPageHeader)
    {
        var pages = new List<List<(Block, float)>> { new() };
        var y = BodyTop + firstPageHeader;
        foreach (var b in blocks)
        {
            var top = pages[^1].Count == 0 ? y : y + b.SpaceBefore;
            if (top + b.Height > Bottom && pages[^1].Count > 0)
            {
                pages.Add(new());
                top = BodyTop;
            }
            pages[^1].Add((b, top));
            y = top + b.Height;
        }
        return pages;
    }

    /// <summary>The title (it may wrap) and the «Αθλητής: …» line, with the space after them.</summary>
    private float FirstPageHeaderHeight(FormDefinition form) =>
        Wrap([new MarkupSpan(form.Title, true)], 19, Right - Left).Count * 19 * 1.45f + 34;

    private List<Block> Layout(FormDefinition form, FormAssignment assignment)
    {
        var width = Right - Left;
        var blocks = new List<Block>();

        // The intro text, line by line, as in the app.
        foreach (var line in WorkoutMarkup.Parse(form.Intro))
        {
            switch (line.Kind)
            {
                case MarkupKind.Blank:
                    blocks.Add(new Block(6, 0, (_, _) => { }));
                    break;
                case MarkupKind.Heading:
                    blocks.Add(TextBlock(line.Spans.Select(s => s with { Bold = true }).ToList(), 13, Navy, Left, width, 10));
                    break;
                case MarkupKind.Bullet:
                case MarkupKind.Numbered:
                    var marker = line.Kind == MarkupKind.Bullet ? "•" : $"{line.Number}.";
                    var item = TextBlock(line.Spans, 10.5f, Ink, Left + 16, width - 16, 3);
                    blocks.Add(item with
                    {
                        Draw = (c, y) =>
                        {
                            using var f = Font(false, 10.5f);
                            using var p = Paint(Ink);
                            c.DrawText(marker, Left + 2, y + f.Size, SKTextAlign.Left, f, p);
                            item.Draw(c, y);
                        },
                    });
                    break;
                default:
                    blocks.Add(TextBlock(line.Spans, 10.5f, Ink, Left, width, 3));
                    break;
            }
        }

        // The questions, numbered, each with its answer in a light box.
        var number = 0;
        foreach (var q in form.Questions)
        {
            number++;
            var label = TextBlock([new MarkupSpan($"{number}. {q.Label}{(q.Required ? " *" : string.Empty)}", true)],
                10.5f, Ink, Left, width, number == 1 ? 18 : 12);
            blocks.Add(label);

            var answer = FormAnswers.Display(q, assignment.Answers.GetValueOrDefault(q.Id));
            var lines = answer.Replace("\r", string.Empty).Split('\n')
                .SelectMany(l => Wrap([new MarkupSpan(l, false)], 10.5f, width - 20)).ToList();
            var lineH = 10.5f * 1.45f;
            var boxH = Math.Max(1, lines.Count) * lineH + 12;
            blocks.Add(new Block(boxH, 4, (c, y) =>
            {
                using var fill = Paint(AnswerFill);
                c.DrawRoundRect(new SKRoundRect(new SKRect(Left, y, Right, y + boxH), 5), fill);
                var ly = y + 6;
                foreach (var l in lines)
                {
                    DrawLine(c, l, Left + 10, ly, 10.5f, Ink);
                    ly += lineH;
                }
            }));
        }
        return blocks;
    }

    private Block TextBlock(IReadOnlyList<MarkupSpan> spans, float size, SKColor color, float x, float width, float spaceBefore)
    {
        var lines = Wrap(spans, size, width);
        var lineH = size * 1.45f;
        return new Block(Math.Max(1, lines.Count) * lineH, spaceBefore, (c, y) =>
        {
            foreach (var l in lines)
            {
                DrawLine(c, l, x, y, size, color);
                y += lineH;
            }
        });
    }

    /// <summary>Word wrap that keeps bold and regular runs apart.</summary>
    private List<List<MarkupSpan>> Wrap(IReadOnlyList<MarkupSpan> spans, float size, float width)
    {
        var lines = new List<List<MarkupSpan>> { new() };
        var used = 0f;
        foreach (var span in spans)
        {
            foreach (var word in SplitKeepingSpaces(span.Text))
            {
                using var f = Font(span.Bold, size);
                var w = f.MeasureText(word);
                var isSpace = word.Trim().Length == 0;
                if (used + w > width && used > 0 && !isSpace)
                {
                    lines.Add(new());
                    used = 0;
                }
                if (used == 0 && isSpace) continue;
                lines[^1].Add(new MarkupSpan(word, span.Bold));
                used += w;
            }
        }
        return lines;
    }

    private static IEnumerable<string> SplitKeepingSpaces(string text)
    {
        var start = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            if (i == text.Length || text[i] == ' ')
            {
                if (i > start) yield return text[start..i];
                if (i < text.Length) yield return " ";
                start = i + 1;
            }
        }
    }

    private void DrawLine(SKCanvas c, List<MarkupSpan> line, float x, float y, float size, SKColor color)
    {
        using var paint = Paint(color);
        foreach (var span in line)
        {
            using var f = Font(span.Bold, size);
            c.DrawText(span.Text, x, y + size, SKTextAlign.Left, f, paint);
            x += f.MeasureText(span.Text);
        }
    }

    // ---------------- page furniture ----------------

    private void Header(SKCanvas c, FormDefinition form, FormAssignment a, string athleteName, bool first)
    {
        // The logo: the app icon's heart line on its gold square.
        var box = new SKRect(Left, Top, Left + 24, Top + 24);
        using (var gold = new SKPaint { IsAntialias = true })
        {
            gold.Shader = SKShader.CreateLinearGradient(new SKPoint(box.Left, box.Top), new SKPoint(box.Right, box.Bottom),
                [SKColor.Parse("#FFD700"), SKColor.Parse("#FFA000")], SKShaderTileMode.Clamp);
            c.DrawRoundRect(new SKRoundRect(box, 6), gold);
        }
        using (var path = SKPath.ParseSvgPathData(LogoPath))
        using (var navy = Paint(Navy))
        {
            c.Save();
            c.Translate(box.Left + 4, box.Top + 4);
            c.Scale(16f / 24f);
            c.DrawPath(path, navy);
            c.Restore();
        }
        using (var f = Font(true, 14))
        using (var p = Paint(Navy))
            c.DrawText("AthloTrack", box.Right + 8, box.Top + 17, SKTextAlign.Left, f, p);
        using (var f = Font(false, 9))
        using (var p = Paint(Muted))
            c.DrawText(first ? "Έντυπο" : form.Title, Right, box.Top + 16, SKTextAlign.Right, f, p);
        using (var rule = new SKPaint { Color = Blue, StrokeWidth = 1.2f, IsAntialias = true })
            c.DrawLine(Left, Top + 34, Right, Top + 34, rule);

        if (!first) return;
        var title = TextBlock([new MarkupSpan(form.Title, true)], 19, Navy, Left, Right - Left, 0);
        title.Draw(c, BodyTop);
        var submitted = a.SubmittedAtLocal is { } at
            ? $" · Υποβλήθηκε {at.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)}"
            : string.Empty;
        using var sf = Font(false, 10);
        using var sp = Paint(Muted);
        c.DrawText($"Αθλητής: {athleteName}{submitted}", Left, BodyTop + title.Height + 12, SKTextAlign.Left, sf, sp);
    }

    private void Footer(SKCanvas c, FormDefinition form, int page, int pages)
    {
        using var rule = new SKPaint { Color = Rule, StrokeWidth = 0.8f };
        c.DrawLine(Left, PageH - 44, Right, PageH - 44, rule);
        using var f = Font(false, 8.5f);
        using var p = Paint(Muted);
        c.DrawText($"AthloTrack · {form.Title}", Left, PageH - 30, SKTextAlign.Left, f, p);
        c.DrawText($"Σελίδα {page} από {pages}", Right, PageH - 30, SKTextAlign.Right, f, p);
    }

    // Unhinted, linear widths: hinting rounds each glyph's advance and spaces the letters out in print.
    private SKFont Font(bool bold, float size) =>
        new(bold ? _bold : _regular, size) { Subpixel = true, LinearMetrics = true, Hinting = SKFontHinting.None };

    private static SKPaint Paint(SKColor color) => new() { Color = color, IsAntialias = true };
}
