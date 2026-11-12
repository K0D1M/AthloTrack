using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AthloTrack.Core.Models;

namespace AthloTrack.Core.Forms;

/// <summary>A field's kind: one line, several lines, a date, a tick box.</summary>
public enum FormFieldKind { Text, Long, Date, Tick }

/// <summary>A field in a form's text: the n-th marker, its kind, and whether it must be filled.</summary>
public sealed record FormField(int Index, FormFieldKind Kind, bool Required)
{
    /// <summary>Its answer's key among the copy's answers: "f1", "f2", … (questions use 8-letter ids).</summary>
    public string Key => FormFields.KeyOf(Index);
}

/// <summary>
/// «Έντυπα» with fields: the coach's text carries markers the athlete fills in, and the final
/// document is that text with the answers in place. A marker reads as it shows in the editor,
/// so a coach can also type one: <c>[[Κείμενο]]</c>, <c>[[Πολλές γραμμές]]</c>,
/// <c>[[Ημερομηνία]]</c>, <c>[[Κουτάκι]]</c>, each optionally <c>(προαιρετικό)</c>.
/// Fields are numbered in reading order; their answers are stored as "f1", "f2", ….
/// </summary>
public static class FormFields
{
    private static readonly (FormFieldKind Kind, string Name)[] Names =
    [
        (FormFieldKind.Text, "Κείμενο"),
        (FormFieldKind.Long, "Πολλές γραμμές"),
        (FormFieldKind.Date, "Ημερομηνία"),
        (FormFieldKind.Tick, "Κουτάκι"),
    ];

    public const string Optional = "προαιρετικό";

    /// <summary>A marker: [[Κείμενο]], [[Κουτάκι (προαιρετικό)]], ….</summary>
    public static readonly Regex Marker = new(
        @"\[\[\s*(Κείμενο|Πολλές γραμμές|Ημερομηνία|Κουτάκι)\s*(\(\s*προαιρετικό\s*\))?\s*\]\]",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string KeyOf(int index) => "f" + (index + 1).ToString(CultureInfo.InvariantCulture);

    /// <summary>«Κείμενο», «Πολλές γραμμές», «Ημερομηνία», «Κουτάκι».</summary>
    public static string Name(FormFieldKind kind) => Names.First(n => n.Kind == kind).Name;

    public static string MarkerFor(FormFieldKind kind, bool required) =>
        required ? $"[[{Name(kind)}]]" : $"[[{Name(kind)} ({Optional})]]";

    /// <summary>The field a marker match stands for (its index is given by the caller's count).</summary>
    public static FormField FromMatch(Match m, int index)
    {
        var name = m.Groups[1].Value;
        var kind = Names.First(n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase)).Kind;
        return new FormField(index, kind, Required: !m.Groups[2].Success);
    }

    /// <summary>The text's fields, in reading order.</summary>
    public static IReadOnlyList<FormField> Parse(string? text) =>
        string.IsNullOrEmpty(text) ? [] : Marker.Matches(text).Select((m, i) => FromMatch(m, i)).ToList();

    public static bool HasFields(string? text) => !string.IsNullOrEmpty(text) && Marker.IsMatch(text);

    /// <summary>One piece of a line: plain text, or a field.</summary>
    public readonly record struct Piece(string Text, FormField? Field);

    /// <summary>
    /// Splits text into plain pieces and fields, numbering the fields from <paramref name="next"/>
    /// on (the count carries on from line to line, so the numbers match <see cref="Parse"/>).
    /// </summary>
    public static List<Piece> Split(string text, ref int next)
    {
        var pieces = new List<Piece>();
        var at = 0;
        foreach (Match m in Marker.Matches(text))
        {
            if (m.Index > at) pieces.Add(new Piece(text[at..m.Index], null));
            pieces.Add(new Piece(string.Empty, FromMatch(m, next++)));
            at = m.Index + m.Length;
        }
        if (at < text.Length) pieces.Add(new Piece(text[at..], null));
        return pieces;
    }

    /// <summary>The text with the n-th marker replaced (an empty <paramref name="marker"/> removes it).</summary>
    public static string Replace(string text, int index, string marker)
    {
        var matches = Marker.Matches(text);
        if (index < 0 || index >= matches.Count) return text;
        var m = matches[index];
        var before = text[..m.Index];
        var after = text[(m.Index + m.Length)..];
        // Removing it: no double space left behind.
        if (marker.Length == 0 && before.EndsWith(' ') && (after.Length == 0 || after.StartsWith(' ') || after.StartsWith('\n')))
            before = before[..^1];
        return before + marker + after;
    }

    // «___/___/____», «__ / __ / ____»: a date; three or more underscores: a line to write on.
    private static readonly Regex DateBlank = new(@"_{2,}\s*/\s*_{2,}\s*/\s*_{2,}");
    private static readonly Regex TextBlank = new(@"_{3,}");

    /// <summary>
    /// A Word document's blanks as fields: «__/__/____» a date, «______» text, ☐ / ☒ a tick box
    /// (optional: in «☐ Ναι ☐ Όχι» only one is ticked). Underscores inside bold are left alone
    /// only if they aren't blanks (they always are).
    /// </summary>
    public static string FromBlanks(string text)
    {
        var s = DateBlank.Replace(text, _ => MarkerFor(FormFieldKind.Date, true));
        s = TextBlank.Replace(s, _ => MarkerFor(FormFieldKind.Text, true));
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (ch is '☐' or '☒' or '☑') sb.Append(MarkerFor(FormFieldKind.Tick, false));
            else sb.Append(ch);
        }
        // A blank inside bold («**Υπογραφή: ______**») would put the marker inside the bold run.
        return Regex.Replace(sb.ToString(), @"\*\*([^*\n]*?)(\[\[[^\]]+\]\])\*\*", "**$1**$2").Replace("****", string.Empty);
    }

    /// <summary>The required fields left empty (a tick box counts when ticked).</summary>
    public static IReadOnlyList<FormField> Missing(string? text, IReadOnlyDictionary<string, string> answers) =>
        Parse(text).Where(f => f.Required && string.IsNullOrWhiteSpace(answers.GetValueOrDefault(f.Key))).ToList();

    /// <summary>A tick box's stored value when ticked.</summary>
    public const string Ticked = FormAnswers.Yes;

    /// <summary>The answer as it reads in the document: the text, «14/03/2004», or "" when empty.</summary>
    public static string Display(FormField field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return field.Kind switch
        {
            FormFieldKind.Date => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : value,
            FormFieldKind.Tick => value == Ticked ? "☒" : string.Empty,
            _ => value.Trim(),
        };
    }
}
