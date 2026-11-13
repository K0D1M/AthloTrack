using System.Globalization;
using System.Text;

namespace AthloTrack.Core.Text;

/// <summary>
/// A Greek name in the vocative, for greetings: «Γιώργος Παπαδόπουλος» → «Γιώργο Παπαδόπουλε».
/// Each word changes by its ending; anything else (women's names, Latin letters, initials) stays.
/// <list type="bullet">
/// <item>-ης / -ής, -ας / -άς: drop the ς (Γιάννη, Κώστα, Λουκά, Προπονητή).</item>
/// <item>-ούς: drop the ς (Ιησού).</item>
/// <item>-ος with two syllables, or three with the stress on the next-to-last: -ο (Γιώργο, Νίκο, Στέλιο, Αλέκο).</item>
/// <item>other -ος: -ε (Παπαδόπουλε, Θεόδωρε, Νικόλαε, Δημήτριε, Αντώνιε, Κωνσταντίνε); -ός → -έ.</item>
/// </list>
/// </summary>
public static class GreekVocative
{
    public static string Of(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var sb = new StringBuilder();
        var word = new StringBuilder();
        foreach (var ch in name.Trim())
        {
            if (char.IsLetter(ch) || ch == '\'' || ch == '’')
            {
                word.Append(ch);
                continue;
            }
            sb.Append(Word(word.ToString())).Append(ch);
            word.Clear();
        }
        return sb.Append(Word(word.ToString())).ToString();
    }

    private const string Vowels = "αεηιουω";

    private static string Word(string w)
    {
        if (w.Length < 3 || !w.All(IsGreek)) return w;
        var plain = Plain(w);
        var upper = char.IsUpper(w[^1]);

        if (plain.EndsWith("ης") || plain.EndsWith("ας") || plain.EndsWith("ους")) return w[..^1];
        if (!plain.EndsWith("ος")) return w;

        var stem = w[..^2];
        var o = w[^2];
        // Stressed ending (-ός): -έ.
        if (o == 'ό') return stem + "έ";
        if (o == 'Ό') return stem + "Έ";

        var (count, stressFromEnd) = Syllables(w);
        var short_ = count <= 2
                     || (count == 3 && stressFromEnd == 2 && !plain.EndsWith("ιος"));
        return stem + (short_ ? (upper ? "Ο" : "ο") : (upper ? "Ε" : "ε"));
    }

    private static bool IsGreek(char c) =>
        c is >= 'Ͱ' and <= 'Ͽ' or >= 'ἀ' and <= '῿' or '\'' or '’';

    /// <summary>Lower case without accents or diaeresis.</summary>
    private static string Plain(string w)
    {
        var d = w.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        // A capital Σ lowercases to σ; at the end of a word it's ς (ΓΙΩΡΓΟΣ → γιωργος).
        if (sb.Length > 0 && sb[^1] == 'σ') sb[^1] = 'ς';
        return sb.ToString();
    }

    /// <summary>
    /// The syllables, and where the stress falls counting from the end (1 = last; 0 = no accent,
    /// e.g. capitals). Diphthongs (αι, ει, οι, ου, αυ, ευ, ηυ, υι) are one syllable, as is an
    /// unstressed ι before a vowel (Στέ-λιος, Αν-τώ-νιος).
    /// </summary>
    private static (int Count, int StressFromEnd) Syllables(string w)
    {
        var lower = w.ToLowerInvariant();
        var plain = Plain(w);
        var groups = new List<bool>(); // one per syllable: whether it carries the accent
        var i = 0;
        while (i < plain.Length)
        {
            if (!Vowels.Contains(plain[i]))
            {
                i++;
                continue;
            }
            var stressed = Accented(lower[i]);
            var j = i + 1;
            if (j < plain.Length && Vowels.Contains(plain[j]))
            {
                var pair = plain.Substring(i, 2);
                var diphthong = pair is "αι" or "ει" or "οι" or "ου" or "αυ" or "ευ" or "ηυ" or "υι" && !Diaeresis(lower[j]);
                var synizesis = plain[i] == 'ι' && !Accented(lower[i]) && !Diaeresis(lower[j]);
                if (diphthong || synizesis)
                {
                    stressed |= Accented(lower[j]);
                    j++;
                }
            }
            groups.Add(stressed);
            i = j;
        }
        var at = groups.LastIndexOf(true);
        return (groups.Count, at < 0 ? 0 : groups.Count - at);
    }

    private static bool Accented(char c) => "άέήίόύώΐΰ".Contains(c);
    private static bool Diaeresis(char c) => "ϊϋΐΰ".Contains(c);
}
