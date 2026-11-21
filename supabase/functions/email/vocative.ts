// A Greek first name in the vocative, for greetings: «Γιώργος» → «Γιώργο», «Θεόδωρος» → «Θεόδωρε».
// The same rules as the app's AthloTrack.Core/Text/GreekVocative.cs (keep the two alike).

const VOWELS = "αεηιουω";
const ACCENTED = "άέήίόύώΐΰ";
const DIAERESIS = "ϊϋΐΰ";

const isGreek = (c: string) => /[Ͱ-Ͽἀ-῿'’]/.test(c);

/** Lower case without accents or diaeresis; a final σ becomes ς. */
function plain(w: string): string {
  let s = w.toLowerCase().normalize("NFD").replace(/[̀-ͯ]/g, "");
  if (s.endsWith("σ")) s = s.slice(0, -1) + "ς";
  return s;
}

/** Syllable count, and where the accent falls counting from the end (1 = last; 0 = none). */
function syllables(w: string): [number, number] {
  const lower = w.toLowerCase();
  const p = plain(w);
  const groups: boolean[] = [];
  let i = 0;
  while (i < p.length) {
    if (!VOWELS.includes(p[i])) { i++; continue; }
    let stressed = ACCENTED.includes(lower[i]);
    let j = i + 1;
    if (j < p.length && VOWELS.includes(p[j])) {
      const pair = p.slice(i, i + 2);
      const diphthong = ["αι", "ει", "οι", "ου", "αυ", "ευ", "ηυ", "υι"].includes(pair) && !DIAERESIS.includes(lower[j]);
      const synizesis = p[i] === "ι" && !ACCENTED.includes(lower[i]) && !DIAERESIS.includes(lower[j]);
      if (diphthong || synizesis) {
        stressed = stressed || ACCENTED.includes(lower[j]);
        j++;
      }
    }
    groups.push(stressed);
    i = j;
  }
  const at = groups.lastIndexOf(true);
  return [groups.length, at < 0 ? 0 : groups.length - at];
}

function word(w: string): string {
  if (w.length < 3 || ![...w].every(isGreek)) return w;
  const p = plain(w);
  const upper = w[w.length - 1] === w[w.length - 1].toUpperCase() && w[w.length - 1] !== w[w.length - 1].toLowerCase();
  if (p.endsWith("ης") || p.endsWith("ας") || p.endsWith("ους")) return w.slice(0, -1);
  if (!p.endsWith("ος")) return w;
  const stem = w.slice(0, -2);
  const o = w[w.length - 2];
  if (o === "ό") return stem + "έ";
  if (o === "Ό") return stem + "Έ";
  const [count, fromEnd] = syllables(w);
  const short = count <= 2 || (count === 3 && fromEnd === 2 && !p.endsWith("ιος"));
  return stem + (short ? (upper ? "Ο" : "ο") : (upper ? "Ε" : "ε"));
}

/** The first word of a full name, in the vocative («Μαρία Κωνσταντίνου» → «Μαρία»). */
export function firstNameVocative(fullName: string | null | undefined): string {
  const first = (fullName ?? "").trim().split(/\s+/)[0] ?? "";
  return word(first);
}
