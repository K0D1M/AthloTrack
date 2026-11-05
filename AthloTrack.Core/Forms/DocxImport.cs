using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace AthloTrack.Core.Forms;

/// <summary>
/// «Εισαγωγή από Word»: a .docx file's text as the app's markup. Title and Heading 1–3 become
/// <c>## </c>, bulleted and numbered lists <c>- </c> / <c>1. </c>, bold runs <c>**…**</c>; a table
/// row becomes one line with its cells joined by « · ». Images and other objects are skipped.
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

        foreach (var block in body.Elements())
        {
            if (block.Name == W + "p")
            {
                lines.Add(Paragraph(block, lists, headings, counters));
            }
            else if (block.Name == W + "tbl")
            {
                foreach (var row in block.Descendants(W + "tr"))
                {
                    var cells = row.Elements(W + "tc")
                        .Select(tc => string.Join(" ", tc.Elements(W + "p").Select(p => Runs(p).Trim()).Where(t => t.Length > 0)))
                        .Where(t => t.Length > 0);
                    var line = string.Join(" · ", cells);
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

    /// <summary>The paragraph's text, with bold runs as <c>**…**</c> (neighbouring bold runs joined).</summary>
    private static string Runs(XElement p)
    {
        var sb = new StringBuilder();
        var boldOpen = false;
        foreach (var r in p.Descendants(W + "r"))
        {
            var text = string.Concat(r.Elements().Select(e =>
                e.Name == W + "t" ? e.Value
                : e.Name == W + "tab" ? " "
                : e.Name == W + "br" || e.Name == W + "cr" ? " "
                : string.Empty));
            if (text.Length == 0) continue;
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
