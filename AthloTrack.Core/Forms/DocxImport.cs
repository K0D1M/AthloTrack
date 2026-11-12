using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace AthloTrack.Core.Forms;

/// <summary>
/// «Εισαγωγή από Word»: a .docx file's text as the app's markup. Title and Heading 1–3 become
/// <c>## </c>, bulleted and numbered lists <c>- </c> / <c>1. </c>, bold runs <c>**…**</c>; a table
/// row becomes one line with its cells joined by « · », an empty cell a blank to fill
/// («Ύψος: ______»). Word's form controls keep their text; its old-style form fields become
/// <c>______</c> (text) or ☐ / ☒ (tick box), as do symbol-font tick boxes. Images are skipped.
/// </summary>
public static class DocxImport
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public static string ToMarkup(Stream docx)
    {
        using var zip = new ZipArchive(docx, ZipArchiveMode.Read, leaveOpen: true);
        var document = Load(zip, "word/document.xml") ?? throw new InvalidDataException("Δεν είναι αρχείο Word (.docx).");
        var numbering = Load(zip, "word/numbering.xml");
        var styles = Load(zip, "word/styles.xml");

        var body = document.Root?.Element(W + "body");
        if (body is null) return string.Empty;
        var lists = new ListFormats(numbering);
        var headings = HeadingStyles(styles);
        var lines = new List<string>();
        var counters = new Dictionary<(string, int), int>();

        foreach (var block in Blocks(body))
        {
            if (block.Name == W + "p")
            {
                lines.Add(Paragraph(block, lists, headings, counters));
            }
            else if (block.Name == W + "tbl")
            {
                foreach (var row in block.Descendants(W + "tr"))
                {
                    var line = Row(row);
                    if (line.Length > 0) lines.Add(line);
                }
                lines.Add(string.Empty);
            }
        }

        // One blank line at most between blocks; none at the ends.
        var result = new List<string>();
        foreach (var line in lines)
        {
            if (line.Length == 0 && (result.Count == 0 || result[^1].Length == 0)) continue;
            result.Add(line);
        }
        while (result.Count > 0 && result[^1].Length == 0) result.RemoveAt(result.Count - 1);
        return string.Join('\n', result);
    }

    /// <summary>The body's paragraphs and tables in order, also those inside Word's form controls (w:sdt).</summary>
    private static IEnumerable<XElement> Blocks(XElement parent)
    {
        foreach (var e in parent.Elements())
        {
            if (e.Name == W + "p" || e.Name == W + "tbl") yield return e;
            else if (e.Name == W + "sdt" && e.Element(W + "sdtContent") is { } content)
                foreach (var inner in Blocks(content)) yield return inner;
        }
    }

    /// <summary>The blank an athlete fills in, for an empty cell or a Word text field.</summary>
    public const string Blank = "______";

    /// <summary>
    /// A table row as one line: its cells joined by « · », an empty cell a blank. A label with
    /// an empty cell next to it reads «Ύψος (cm): ______». A row with no text at all is skipped.
    /// </summary>
    private static string Row(XElement row)
    {
        var cells = row.Elements(W + "tc").Select(Cell).ToList();
        if (cells.All(c => c.Length == 0)) return string.Empty;
        if (cells.Count == 2 && cells[0].Length > 0 && cells[0] != Blank && (cells[1].Length == 0 || cells[1] == Blank))
            return cells[0].TrimEnd(':', ' ') + ": " + Blank;
        return string.Join(" · ", cells.Select(c => c.Length == 0 ? Blank : c));
    }

    /// <summary>A cell's text: its paragraphs (also inside form controls), not those of a table nested in it.</summary>
    private static string Cell(XElement tc) =>
        string.Join(" ", tc.Descendants(W + "p")
            .Where(p => p.Ancestors(W + "tc").First() == tc)
            .Select(p => Runs(p).Trim())
            .Where(t => t.Length > 0));

    private static string Paragraph(XElement p, ListFormats lists, HashSet<string> headings,
        Dictionary<(string, int), int> counters)
    {
        var text = Runs(p).Trim();
        if (text.Length == 0) return string.Empty;

        var props = p.Element(W + "pPr");
        var style = props?.Element(W + "pStyle")?.Attribute(W + "val")?.Value ?? string.Empty;
        if (headings.Contains(style) || style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase)
            || style.Equals("Title", StringComparison.OrdinalIgnoreCase))
            return "## " + text.Replace("**", string.Empty);

        var num = props?.Element(W + "numPr");
        var numId = num?.Element(W + "numId")?.Attribute(W + "val")?.Value;
        if (numId is not null && numId != "0")
        {
            var level = int.TryParse(num!.Element(W + "ilvl")?.Attribute(W + "val")?.Value, out var l) ? l : 0;
            // The app's lists have no nesting: deeper items are listed as items too.
            if (lists.IsBullet(numId, level)) return $"- {text}";
            var key = (numId, level);
            counters[key] = counters.GetValueOrDefault(key) + 1;
            foreach (var deeper in counters.Keys.Where(k => k.Item1 == numId && k.Item2 > level).ToList()) counters.Remove(deeper);
            return $"{counters[key]}. {text}";
        }

        return text;
    }

    /// <summary>
    /// The paragraph's text, with bold runs as <c>**…**</c> (neighbouring bold runs joined). An
    /// old-style form field (FORMTEXT / FORMCHECKBOX) becomes a blank or a tick box, its shown
    /// value dropped (usually spaces, or the default the author typed); an empty form control
    /// (showing Word's grey «Κάντε κλικ εδώ…» placeholder) becomes a blank.
    /// </summary>
    private static string Runs(XElement p)
    {
        var sb = new StringBuilder();
        var boldOpen = false;
        var inField = false;    // between a form field's «separate» and «end»: its shown value
        var afterField = false; // the next text needs a space after the field
        var placeholders = new HashSet<XElement>();

        void Field(string field)
        {
            if (sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
            sb.Append(field);
            afterField = true;
        }

        foreach (var r in p.Descendants(W + "r"))
        {
            if (r.Element(W + "fldChar") is { } fld)
            {
                var kind = fld.Attribute(W + "fldCharType")?.Value;
                if (kind == "begin" && fld.Element(W + "ffData") is { } ff)
                {
                    var box = ff.Element(W + "checkBox");
                    var on = box?.Element(W + "default")?.Attribute(W + "val")?.Value is "1" or "true"
                             || box?.Element(W + "checked") is { } c && c.Attribute(W + "val")?.Value is null or "1" or "true";
                    Field(box is not null ? (on ? "☒" : "☐") : Blank);
                    inField = true;
                }
                else if (kind == "end")
                {
                    inField = false;
                }
                continue;
            }
            if (inField) continue;

            // An empty form control: one blank for its placeholder text.
            if (r.Ancestors(W + "sdt").FirstOrDefault(s => s.Element(W + "sdtPr")?.Element(W + "showingPlcHdr") is not null) is { } empty)
            {
                if (placeholders.Add(empty)) Field(Blank);
                continue;
            }

            var text = string.Concat(r.Elements().Select(e =>
                e.Name == W + "t" ? e.Value
                : e.Name == W + "tab" ? " "
                : e.Name == W + "br" || e.Name == W + "cr" ? " "
                : e.Name == W + "sym" ? Symbol(e.Attribute(W + "char")?.Value)
                : string.Empty));
            if (text.Length == 0) continue;
            if (afterField && !char.IsWhiteSpace(text[0])) sb.Append(' ');
            afterField = false;
            var b = r.Element(W + "rPr")?.Element(W + "b");
            var bold = b is not null && b.Attribute(W + "val")?.Value is null or "1" or "true" or "on";
            if (bold && text.Trim().Length == 0) bold = boldOpen;
            if (bold != boldOpen)
            {
                if (boldOpen) CloseBold(sb);
                else sb.Append("**");
                boldOpen = bold;
            }
            sb.Append(text);
        }
        if (boldOpen) CloseBold(sb);
        return sb.ToString().Replace("****", string.Empty);
    }

    /// <summary>
    /// A symbol-font character (w:sym): tick boxes as ☐ / ☒, whether drawn with a Unicode box
    /// (MS Gothic, Segoe UI Symbol) or Wingdings' private codes; anything else is dropped.
    /// </summary>
    internal static string Symbol(string? hex)
    {
        if (!int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var code)) return string.Empty;
        return code switch
        {
            0x2610 or 0xF0A8 or 0xF06F or 0xF071 or 0xF072 => "☐",
            0x2611 or 0x2612 or 0xF0FE or 0xF078 or 0xF0FD => "☒",
            _ => string.Empty,
        };
    }

    // «**λέξη **» → «**λέξη** »: markup bold must hug its text.
    private static void CloseBold(StringBuilder sb)
    {
        var trailing = 0;
        while (sb.Length > trailing && sb[sb.Length - 1 - trailing] == ' ') trailing++;
        sb.Length -= trailing;
        sb.Append("**").Append(' ', trailing);
    }

    private static XDocument? Load(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path);
        if (entry is null) return null;
        using var s = entry.Open();
        return XDocument.Load(s);
    }

    /// <summary>Style ids whose name is Title or Heading 1–3 (ids are localized, e.g. «Επικεφαλίδα1»).</summary>
    private static HashSet<string> HeadingStyles(XDocument? styles)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (styles?.Root is null) return ids;
        foreach (var s in styles.Root.Elements(W + "style"))
        {
            var name = s.Element(W + "name")?.Attribute(W + "val")?.Value ?? string.Empty;
            if (name.Equals("Title", StringComparison.OrdinalIgnoreCase)
                || name.Equals("heading 1", StringComparison.OrdinalIgnoreCase)
                || name.Equals("heading 2", StringComparison.OrdinalIgnoreCase)
                || name.Equals("heading 3", StringComparison.OrdinalIgnoreCase))
                ids.Add(s.Attribute(W + "styleId")?.Value ?? string.Empty);
        }
        return ids;
    }

    /// <summary>numbering.xml: which list (numId) and level is bulleted rather than numbered.</summary>
    private sealed class ListFormats
    {
        private readonly Dictionary<string, string> _abstractOf = new();
        private readonly Dictionary<(string, int), string> _format = new();

        public ListFormats(XDocument? numbering)
        {
            if (numbering?.Root is null) return;
            foreach (var a in numbering.Root.Elements(W + "abstractNum"))
            {
                var id = a.Attribute(W + "abstractNumId")?.Value ?? string.Empty;
                foreach (var lvl in a.Elements(W + "lvl"))
                {
                    var level = int.TryParse(lvl.Attribute(W + "ilvl")?.Value, out var l) ? l : 0;
                    _format[(id, level)] = lvl.Element(W + "numFmt")?.Attribute(W + "val")?.Value ?? "decimal";
                }
            }
            foreach (var n in numbering.Root.Elements(W + "num"))
            {
                var numId = n.Attribute(W + "numId")?.Value;
                var abstractId = n.Element(W + "abstractNumId")?.Attribute(W + "val")?.Value;
                if (numId is not null && abstractId is not null) _abstractOf[numId] = abstractId;
            }
        }

        public bool IsBullet(string numId, int level) =>
            !_abstractOf.TryGetValue(numId, out var a) || !_format.TryGetValue((a, level), out var f) || f is "bullet" or "none";
    }
}
