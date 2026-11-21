// Writes every AthloTrack email, filled with sample data, to supabase/functions/email/preview/,
// for opening in a browser. Nothing is sent. Run from the repository root:
//   node supabase/functions/email/preview.mjs
// (Node 23.6+ reads the .ts templates directly.)

import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { inviteEmail, linkCodeEmail, newCoachEmail, resetEmail } from "./templates.ts";
import { firstNameVocative } from "./vocative.ts";

const here = dirname(fileURLToPath(import.meta.url));
const out = join(here, "preview");
mkdirSync(out, { recursive: true });

const samples = {
  reset: resetEmail({ code: "481927" }),
  "link-code": linkCodeEmail({ code: "730512", name: firstNameVocative("Μαρία Κωνσταντίνου"), coach: "Νίκος Αλεξίου" }),
  invite: inviteEmail({ name: firstNameVocative("Γιώργος Παπαδάκης"), coach: "Νίκος Αλεξίου", email: "giorgos@example.com" }),
  "new-coach": newCoachEmail({ name: firstNameVocative("Νίκος Αλεξίου"), email: "nikos@example.com", password: "kR7pm2Qx9wZt" }),
};

for (const [name, mail] of Object.entries(samples)) {
  writeFileSync(join(out, `${name}.html`), mail.html);
  writeFileSync(join(out, `${name}.txt`), `Θέμα: ${mail.subject}\n\n${mail.text}\n`);
  console.log(`${name}: ${mail.subject}`);
}
console.log(`\nOpen the files in ${out}`);
