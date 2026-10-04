using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AthloTrack.Core.Workouts;

public enum MarkupKind
{
    Text,
    Heading,
    Bullet,
    Numbered,
    Blank,
}

public sealed record MarkupSpan(string Text, bool Bold);

/// <param name="Number">The item number of a <see cref="MarkupKind.Numbered"/> line, otherwise 0.</param>
public sealed record MarkupLine(MarkupKind Kind, int Number, IReadOnlyList<MarkupSpan> Spans);

/// <summary>An editor change: the new text and where the selection goes.</summary>
public readonly record struct TextEdit(string Text, int SelectionStart, int SelectionEnd);

/// <summary>
/// The small markup of workout texts, stored as plain text in <c>workout_programs.content</c>:
/// <c>## Τίτλος</c> heading, <c>- </c> (or <c>• </c>) bullet, <c>1. </c> numbered item,
/// <c>**bold**</c>. Any other line is shown exactly as typed, so older workouts look the same.
/// Also the editing helpers behind the editor's toolbar.
/// </summary>
public static partial class WorkoutMarkup
{
    [GeneratedRegex(@"^(\d{1,3})[.)]\s+")]
    private static partial Regex NumberedPrefix();

    private static readonly CultureInfo Greek = CultureInfo.GetCultureInfo("el-GR");

    public static IReadOnlyList<MarkupLine> Parse(string? text)
    {
        var lines = new List<MarkupLine>();
        if (string.IsNullOrEmpty(text)) return lines;

        foreach (var raw in text.Replace("\r", string.Empty).Split('\n'))
        {
            var (kind, number, content) = Classify(raw);
            lines.Add(new MarkupLine(kind, number, kind == MarkupKind.Blank ? [] : ParseInline(content)));
        }

        return lines;
    }

    /// <summary>The text without markup (bullets as •), for short previews such as the calendar.</summary>
    public static string PlainPreview(string? text)
    {
        var sb = new StringBuilder();
        foreach (var line in Parse(text))
        {
            if (line.Kind == MarkupKind.Blank) continue;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(line.Kind switch
            {
                MarkupKind.Bullet => "• ",
                MarkupKind.Numbered => $"{line.Number}. ",
                _ => string.Empty,
            });
            foreach (var span in line.Spans) sb.Append(span.Text);
        }

        return sb.ToString();
    }

    /// <summary>Wraps the selection in <c>**…**</c>, or unwraps it if it already is bold.</summary>
    public static TextEdit ToggleBold(string text, int start, int end)
    {
        (start, end) = Order(text, start, end);
        if (start == end)
            return new TextEdit(text.Insert(start, "****"), start + 2, start + 2);

        var selected = text[start..end];
        if (selected.Length >= 4 && selected.StartsWith("**") && selected.EndsWith("**"))
            return new TextEdit(text.Remove(end - 2, 2).Remove(start, 2), start, end - 4);

        if (start >= 2 && end + 2 <= text.Length && text.Substring(start - 2, 2) == "**" && text.Substring(end, 2) == "**")
            return new TextEdit(text.Remove(end, 2).Remove(start - 2, 2), start - 2, end - 2);

        return new TextEdit(text.Insert(end, "**").Insert(start, "**"), start + 2, end + 2);
    }

    /// <summary>
    /// Makes every selected line a heading, bullet or numbered item (numbered 1, 2, 3…), replacing
    /// another kind of prefix. If all of them already are that kind, removes it instead.
    /// </summary>
    public static TextEdit ToggleLinePrefix(string text, int start, int end, MarkupKind kind)
    {
        if (kind is not (MarkupKind.Heading or MarkupKind.Bullet or MarkupKind.Numbered))
            throw new ArgumentOutOfRangeException(nameof(kind));

        (start, end) = Order(text, start, end);
        var lineStart = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;
        // A selection that ends right after a line break doesn't include the next line.
        var searchFrom = end > start && text[end - 1] == '\n' ? end - 1 : end;
        var nextBreak = text.IndexOf('\n', Math.Min(searchFrom, text.Length));
        var lineEnd = nextBreak < 0 ? text.Length : nextBreak;

        var lines = text[lineStart..lineEnd].Split('\n');
        var filled = lines.Where(l => !string.IsNullOrWhiteSpace(l.TrimEnd('\r'))).ToList();
        var remove = filled.Count > 0 && filled.All(l => Classify(l.TrimEnd('\r')).Kind == kind);

        var number = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var cr = lines[i].EndsWith('\r') ? "\r" : string.Empty;
            var line = lines[i].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line) && lines.Length > 1) continue; // keep gaps as they are

            var content = StripPrefix(line);
            lines[i] = (remove ? content : kind switch
            {
                MarkupKind.Heading => "## " + content,
                MarkupKind.Bullet => "- " + content,
                _ => $"{++number}. " + content,
            }) + cr;
        }

        var block = string.Join('\n', lines);
        var result = text[..lineStart] + block + text[lineEnd..];
        return start == end
            ? new TextEdit(result, lineStart + block.TrimEnd('\r').Length, lineStart + block.TrimEnd('\r').Length)
            : new TextEdit(result, lineStart, lineStart + block.Length);
    }

    /// <summary>
    /// After a line break was typed at the end of a list item, starts the next item (the same bullet,
    /// or the next number). On an empty item it removes the prefix instead, which ends the list.
    /// Works from the text change, not the Enter key, so phones' keyboards behave the same.
    /// Returns null when the change wasn't a line break after a list item.
    /// </summary>
    public static TextEdit? ContinueList(string? oldText, string? newText, int caret)
    {
        oldText ??= string.Empty;
        newText ??= string.Empty;
        var added = newText.Length - oldText.Length;
        if (added is not (1 or 2) || caret < added || caret > newText.Length) return null;

        var inserted = newText.Substring(caret - added, added);
        if (inserted is not ("\n" or "\r\n") || newText.Remove(caret - added, added) != oldText) return null;

        var breakAt = caret - added;
        var prevStart = breakAt == 0 ? 0 : oldText.LastIndexOf('\n', breakAt - 1) + 1;
        var prevLine = oldText[prevStart..breakAt].TrimEnd('\r');
        var (kind, number, content) = Classify(prevLine);
        if (kind is not (MarkupKind.Bullet or MarkupKind.Numbered)) return null;

        if (string.IsNullOrWhiteSpace(content))
        {
            // Empty item + Enter: drop the prefix and the new line, leaving an empty line.
            var withoutItem = newText.Remove(prevStart, caret - prevStart);
            return new TextEdit(withoutItem, prevStart, prevStart);
        }

        var prefix = kind == MarkupKind.Bullet ? prevLine.TrimStart()[..2] : $"{number + 1}. ";
        return new TextEdit(newText.Insert(caret, prefix), caret + prefix.Length, caret + prefix.Length);
    }

    /// <summary>
    /// One exercise as a bullet line, e.g. <c>- **Καθίσματα** — 3×10 @ 60kg, διάλ. 90"</c> or
    /// <c>- **Τρέξιμο** — 20' / 5km</c>. Empty fields are left out.
    /// </summary>
    public static string FormatExercise(string name, int? sets, int? reps, decimal? kg, int? minutes,
        string? distance, int? restSeconds)
    {
        var parts = new List<string>();
        var load = (sets, reps) switch
        {
            ({ } s, { } r) => $"{s}×{r}",
            ({ } s, null) => $"{s} σετ",
            (null, { } r) => $"{r} επαν.",
            _ => null,
        };
        var weight = kg is { } k ? k.ToString("0.##", Greek) + "kg" : null;
        if (load is not null) parts.Add(weight is null ? load : $"{load} @ {weight}");
        else if (weight is not null) parts.Add(weight);
        if (minutes is { } m) parts.Add($"{m}'");
        if (!string.IsNullOrWhiteSpace(distance)) parts.Add(distance.Trim());

        var line = $"- **{name.Trim()}**";
        var detail = string.Join(" / ", parts);
        var rest = restSeconds is { } r2 ? $"διάλ. {r2}\"" : null;
        if (detail.Length > 0 && rest is not null) return $"{line} — {detail}, {rest}";
        if (detail.Length > 0) return $"{line} — {detail}";
        return rest is not null ? $"{line} — {rest}" : line;
    }

    private static (MarkupKind Kind, int Number, string Content) Classify(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0) return (MarkupKind.Blank, 0, string.Empty);
        if (trimmed.StartsWith("## ")) return (MarkupKind.Heading, 0, trimmed[3..]);
        if (trimmed.StartsWith("- ") || trimmed.StartsWith("• ")) return (MarkupKind.Bullet, 0, trimmed[2..]);
        var numbered = NumberedPrefix().Match(trimmed);
        if (numbered.Success)
            return (MarkupKind.Numbered, int.Parse(numbered.Groups[1].Value, CultureInfo.InvariantCulture), trimmed[numbered.Length..]);
        return (MarkupKind.Text, 0, line);
    }

    private static string StripPrefix(string line)
    {
        var (kind, _, content) = Classify(line);
        return kind is MarkupKind.Heading or MarkupKind.Bullet or MarkupKind.Numbered ? content : line;
    }

    /// <summary>Splits on <c>**</c>; an unclosed <c>**</c> stays as typed.</summary>
    private static IReadOnlyList<MarkupSpan> ParseInline(string content)
    {
        var parts = content.Split("**");
        if (parts.Length % 2 == 0)
        {
            // Odd number of markers: the last one has no partner, so it's plain text.
            parts[^2] = parts[^2] + "**" + parts[^1];
            Array.Resize(ref parts, parts.Length - 1);
        }

        var spans = new List<MarkupSpan>();
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0) spans.Add(new MarkupSpan(parts[i], i % 2 == 1));
        }

        return spans;
    }

    private static (int, int) Order(string text, int start, int end)
    {
        start = Math.Clamp(start, 0, text.Length);
        end = Math.Clamp(end, 0, text.Length);
        return start <= end ? (start, end) : (end, start);
    }
}
