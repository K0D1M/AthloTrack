// AthloTrack emails: the look and the words. Edit here, then preview and redeploy (see
// docs/administration.md, «Αλλαγή εμφάνισης των email»):
//   node supabase/functions/email/preview.mjs     -> supabase/functions/email/preview/*.html
//
// Plain tables and inline styles on purpose: Gmail, Outlook and phone mail apps show them alike.
// {{name}} placeholders are filled by render(); values are HTML-escaped there.

export const FROM = "Εφαρμογή Athlotrack <no-reply@athlotrack.gr>";
export const APP_URL = "https://athlotrack.gr";
const LOGO_URL = "https://athlotrack.gr/icon-192.png";

// Brand colours (the app's Theme.axaml)
const NAVY = "#003366";
const BLUE = "#0D47A1";
const TEXT = "#1C2430";
const MUTED = "#5B6573";
const TINT = "#EEF3FB";

/** The shared frame: header with the logo, the body, the footer. */
const layout = (body: string) => `<!doctype html>
<html lang="el">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="color-scheme" content="light only">
<title>AthloTrack</title>
</head>
<body style="margin:0;padding:0;background:#E9EDF3;">
<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#E9EDF3;">
<tr><td align="center" style="padding:24px 12px;">
  <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="width:100%;max-width:600px;background:#FFFFFF;border-radius:16px;overflow:hidden;font-family:'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:${TEXT};">
    <tr><td bgcolor="${BLUE}" style="background:${BLUE};background-image:linear-gradient(135deg,${NAVY} 0%,${BLUE} 60%,#1565C0 100%);padding:20px 32px;">
      <table role="presentation" cellpadding="0" cellspacing="0"><tr>
        <td style="padding-right:12px;"><img src="${LOGO_URL}" width="44" height="44" alt="AthloTrack" style="display:block;border:0;border-radius:13px;"></td>
        <td style="font-size:20px;font-weight:800;color:#FFFFFF;">AthloTrack</td>
      </tr></table>
    </td></tr>
    <tr><td style="padding:30px 32px 26px;font-size:15px;line-height:1.6;">
${body}
    </td></tr>
    <tr><td style="border-top:1px solid #E3E8F0;padding:16px 32px 20px;font-size:12px;line-height:1.6;color:${MUTED};">
      Το AthloTrack σού έστειλε αυτό το email επειδή χρησιμοποιείται για τον λογαριασμό σου. Μην απαντήσεις σε αυτό: δεν το διαβάζει κανείς.<br>
      <a href="${APP_URL}" style="color:${BLUE};">athlotrack.gr</a>
    </td></tr>
  </table>
</td></tr>
</table>
</body>
</html>`;

const title = (text: string) =>
  `<div style="font-size:22px;font-weight:800;color:${NAVY};line-height:1.3;margin:0 0 16px;">${text}</div>`;
const para = (text: string) => `<p style="margin:0 0 16px;">${text}</p>`;
const note = (text: string) => `<p style="margin:0;font-size:14px;color:#3E4A59;">${text}</p>`;
const codeBox = (code: string) =>
  `<div style="display:inline-block;margin:0 0 16px;background:${TINT};border:1px solid #CFE0F7;border-radius:14px;padding:14px 22px;font-size:32px;font-weight:800;letter-spacing:10px;color:${NAVY};">${code}</div>`;
const button = (text: string, url: string) =>
  `<table role="presentation" cellpadding="0" cellspacing="0" style="margin:0 0 16px;"><tr><td bgcolor="#FFC107" style="border-radius:26px;background:#FFC107;background-image:linear-gradient(135deg,#FFD54F,#FFB300);">
    <a href="${url}" style="display:inline-block;padding:14px 26px;font-size:16px;font-weight:800;color:${TEXT};text-decoration:none;">${text}</a>
  </td></tr></table>`;

/** «481927» → «481 927», «64794670» → «6479 4670»: easier to read and type. */
const spaced = (code: string) => {
  const half = Math.ceil(code.length / 2);
  return code.length >= 6 ? `${code.slice(0, half)} ${code.slice(half)}` : code;
};

export type Email = { subject: string; html: string; text: string };

// ---- The emails ----------------------------------------------------------------------------

export function resetEmail(v: { code: string }): Email {
  const e = esc(v);
  return {
    subject: `Ο κωδικός σου για νέο κωδικό πρόσβασης: ${spaced(v.code)}`,
    html: layout(
      title("Νέος κωδικός πρόσβασης") +
        para("Γεια σου! Ζήτησες να αλλάξεις τον κωδικό σου στο AthloTrack.<br>Γράψε αυτόν τον κωδικό στην εφαρμογή:") +
        codeBox(spaced(e.code)) +
        note("Ισχύει για 1 ώρα. Αν δεν το ζήτησες εσύ, αγνόησέ το: ο κωδικός σου δεν αλλάζει."),
    ),
    text: `Νέος κωδικός πρόσβασης\n\nΓράψε αυτόν τον κωδικό στην εφαρμογή AthloTrack: ${v.code}\n\n` +
      `Ισχύει για 1 ώρα. Αν δεν το ζήτησες εσύ, αγνόησέ το: ο κωδικός σου δεν αλλάζει.\n\n${APP_URL}`,
  };
}

export function linkCodeEmail(v: { code: string; name: string; coach: string }): Email {
  const e = esc(v);
  const hello = e.name ? `${e.name}, για` : "Για";
  const helloText = v.name ? `${v.name}, για` : "Για";
  return {
    subject: `Επιβεβαίωσε το email σου: ${spaced(v.code)}`,
    html: layout(
      title("Επιβεβαίωσε το email σου") +
        para(`${hello} να συνδεθεί ο λογαριασμός σου με το προφίλ που έφτιαξε ο προπονητής σου, ${e.coach}, γράψε αυτόν τον κωδικό στην εφαρμογή:`) +
        codeBox(spaced(e.code)) +
        note("Ισχύει για 15 λεπτά. Αν δεν έφτιαξες εσύ λογαριασμό στο AthloTrack, αγνόησέ το."),
    ),
    text: `Επιβεβαίωσε το email σου\n\n${helloText} να συνδεθεί ο λογαριασμός σου με το προφίλ που έφτιαξε ο προπονητής σου, ` +
      `${v.coach}, γράψε αυτόν τον κωδικό στην εφαρμογή: ${v.code}\n\nΙσχύει για 15 λεπτά.\n\n${APP_URL}`,
  };
}

export function inviteEmail(v: { name: string; coach: string; email: string }): Email {
  const e = esc(v);
  const hello = e.name ? `Γεια σου ${e.name}!` : "Γεια σου!";
  const helloText = v.name ? `Γεια σου ${v.name}!` : "Γεια σου!";
  return {
    subject: `Πρόσκληση στο AthloTrack · ${v.coach}`,
    html: layout(
      title("Σε περιμένουν τα ασκησιολόγιά σου") +
        para(`${hello}<br>Ο προπονητής σου, ${e.coach}, σε πρόσθεσε στο AthloTrack. Εκεί θα βλέπεις τα ασκησιολόγια, τις μετρήσεις και την πρόοδό σου.`) +
        button("Δημιούργησε λογαριασμό", APP_URL) +
        note(`Χρησιμοποίησε αυτό το email (${e.email}) όταν φτιάξεις τον λογαριασμό σου, στο athlotrack.gr ή στην εφαρμογή Android.`),
    ),
    text: `${helloText}\n\nΟ προπονητής σου, ${v.coach}, σε πρόσθεσε στο AthloTrack. Εκεί θα βλέπεις τα ασκησιολόγια, ` +
      `τις μετρήσεις και την πρόοδό σου.\n\nΔημιούργησε λογαριασμό: ${APP_URL}\n` +
      `Χρησιμοποίησε αυτό το email (${v.email}).`,
  };
}

export function newCoachEmail(v: { name: string; email: string; password: string }): Email {
  const e = esc(v);
  const hello = e.name ? `Καλώς ήρθες, ${e.name}!` : "Καλώς ήρθες!";
  const helloText = v.name ? `Καλώς ήρθες, ${v.name}!` : "Καλώς ήρθες!";
  const box = `<div style="margin:0 0 16px;background:${TINT};border:1px solid #CFE0F7;border-radius:14px;padding:14px 18px;font-size:15px;">
      Email: <b>${e.email}</b><br>
      Προσωρινός κωδικός: <b style="font-size:18px;letter-spacing:1px;color:${NAVY};font-family:Consolas,Menlo,monospace;">${e.password}</b>
    </div>`;
  return {
    subject: "Καλώς ήρθες στο AthloTrack",
    html: layout(
      title(hello) +
        para("Δημιουργήθηκε λογαριασμός προπονητή για σένα στο AthloTrack. Συνδέσου με τα παρακάτω:") +
        box +
        button("Άνοιξε το AthloTrack", APP_URL) +
        note("Με την πρώτη σύνδεση θα σου ζητηθεί να αλλάξεις κωδικό."),
    ),
    text: `${helloText}\n\nΔημιουργήθηκε λογαριασμός προπονητή για σένα στο AthloTrack.\n\nEmail: ${v.email}\n` +
      `Προσωρινός κωδικός: ${v.password}\n\nΣύνδεση: ${APP_URL}\nΜε την πρώτη σύνδεση θα σου ζητηθεί να αλλάξεις κωδικό.`,
  };
}

/** HTML-escapes every value, so names can't inject markup (HTML only; the text part stays raw). */
export function esc<T extends Record<string, string>>(v: T): T {
  const out: Record<string, string> = {};
  for (const [k, s] of Object.entries(v)) {
    out[k] = s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
  }
  return out as T;
}
