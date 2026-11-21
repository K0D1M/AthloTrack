// AthloTrack email sender (Supabase Edge Function, Deno), through Resend.
// Deploy with "Verify JWT" OFF: each action checks its caller itself.
//
// Actions (POST JSON { action, ... }):
//   reset        { email }               anyone        -> { ok }  6-digit code for a new password
//                                                                  (always ok: doesn't reveal accounts)
//   link-code    {}                      signed in     -> { match, coach? }  code to link the login to
//                                                                  the athlete profile with its email
//   link-verify  { code }                signed in     -> { linked } | 400 { error, remaining }
//   invite       { athlete_id }          DB trigger    -> { sent }  (header x-email-hook)
//   new-coach    { email, full_name, password }  service role -> { sent }  (the admin function)
//
// Secrets (Edge Functions > Secrets):
//   RESEND_API_KEY     a Resend key with sending access to athlotrack.gr
//   EMAIL_HOOK_SECRET  the same value as Vault's 'email_hook_secret' (022_email_and_linking.sql)
// Provided by Supabase automatically: SUPABASE_URL, SUPABASE_SERVICE_ROLE_KEY
//
// The look and words of every email: templates.ts.

import { createClient } from "npm:@supabase/supabase-js@2";
import { FROM, inviteEmail, linkCodeEmail, newCoachEmail, resetEmail, type Email } from "./templates.ts";
import { firstNameVocative } from "./vocative.ts";

const SUPABASE_URL = Deno.env.get("SUPABASE_URL")!;
const SERVICE_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
const RESEND_KEY = Deno.env.get("RESEND_API_KEY") ?? "";
const HOOK_SECRET = Deno.env.get("EMAIL_HOOK_SECRET") ?? "";
const db = createClient(SUPABASE_URL, SERVICE_KEY, { auth: { persistSession: false } });

const cors = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, apikey, content-type, x-client-info",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};
const reply = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { ...cors, "Content-Type": "application/json" } });
const fail = (message: string, status = 400, extra: Record<string, unknown> = {}) =>
  reply({ error: message, ...extra }, status);

const LINK_MINUTES = 15;
const LINK_TRIES = 5;

// ---- helpers ---------------------------------------------------------------------------------

/** Constant-time string comparison (secrets, code hashes). */
function same(a: string, b: string): boolean {
  if (!a || a.length !== b.length) return false;
  let diff = 0;
  for (let i = 0; i < a.length; i++) diff |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return diff === 0;
}

async function sha256(text: string): Promise<string> {
  const bytes = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(text));
  return Array.from(new Uint8Array(bytes), (b) => b.toString(16).padStart(2, "0")).join("");
}

function sixDigits(): string {
  const n = crypto.getRandomValues(new Uint32Array(1))[0] % 1_000_000;
  return n.toString().padStart(6, "0");
}

async function send(to: string, mail: Email): Promise<void> {
  if (!RESEND_KEY) throw new Error("RESEND_API_KEY is not set");
  const res = await fetch("https://api.resend.com/emails", {
    method: "POST",
    headers: { Authorization: `Bearer ${RESEND_KEY}`, "Content-Type": "application/json" },
    body: JSON.stringify({ from: FROM, to: [to], subject: mail.subject, html: mail.html, text: mail.text }),
  });
  if (!res.ok) throw new Error(`resend ${res.status}: ${await res.text()}`);
}

/** At most one email of a kind per address a minute, and five an hour. */
async function allowed(kind: string, email: string): Promise<boolean> {
  const hourAgo = new Date(Date.now() - 3600_000).toISOString();
  const { data, error } = await db.from("email_log").select("created_at")
    .eq("kind", kind).eq("email", email).gte("created_at", hourAgo)
    .order("created_at", { ascending: false }).limit(5);
  if (error) throw error;
  if ((data?.length ?? 0) >= 5) return false;
  const last = data?.[0]?.created_at;
  return !last || Date.now() - new Date(last).getTime() >= 60_000;
}

async function logSent(kind: string, email: string) {
  const { error } = await db.from("email_log").insert({ kind, email });
  if (error) console.error("email_log", error);
}

/** The signed-in caller (the app sends the user's access token). */
async function caller(req: Request) {
  const token = (req.headers.get("Authorization") ?? "").replace(/^Bearer\s+/i, "");
  if (!token) return null;
  const { data, error } = await db.auth.getUser(token);
  return error ? null : data.user;
}

/** The athlete profile this login may claim: same email, not linked to anyone yet. */
async function claimable(email: string) {
  const { data, error } = await db.from("athletes")
    .select("id, full_name, coach_id, auth_user_id, coaches(full_name)")
    .ilike("email", email.replace(/[%_\\]/g, "\\$&"))
    .is("auth_user_id", null)
    .limit(1).maybeSingle();
  if (error) throw error;
  return data as null | { id: string; full_name: string; coaches: { full_name: string } | null };
}

/** Already a coach, an admin or a linked athlete: nothing to claim. */
async function hasProfile(userId: string): Promise<boolean> {
  for (const table of ["athletes", "coaches", "admins"]) {
    const { count } = await db.from(table).select("id", { count: "exact", head: true }).eq("auth_user_id", userId);
    if ((count ?? 0) > 0) return true;
  }
  return false;
}

// ---- actions ---------------------------------------------------------------------------------

async function reset(body: Record<string, unknown>) {
  const email = String(body.email ?? "").trim().toLowerCase();
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) return fail("Μη έγκυρο email.");
  if (!(await allowed("reset", email))) {
    return fail("Πάρα πολλές προσπάθειες. Δοκίμασε ξανά σε λίγα λεπτά.", 429);
  }
  // A recovery code without Supabase sending anything; no such user: answer ok all the same.
  const { data, error } = await db.auth.admin.generateLink({ type: "recovery", email });
  const code = data?.properties?.email_otp;
  if (!error && code) {
    await send(email, resetEmail({ code }));
    await logSent("reset", email);
  }
  return reply({ ok: true });
}

async function linkCode(req: Request) {
  const user = await caller(req);
  if (!user?.email) return fail("Χρειάζεται σύνδεση.", 401);
  if (await hasProfile(user.id)) return reply({ match: false, linked: true });

  const email = user.email.toLowerCase();
  const athlete = await claimable(email);
  if (!athlete) return reply({ match: false });
  const coach = athlete.coaches?.full_name ?? "ο προπονητής σου";
  if (!(await allowed("link-code", email))) {
    return reply({ match: true, coach, throttled: true });
  }

  const code = sixDigits();
  const { error } = await db.from("athlete_link_codes").upsert({
    auth_user_id: user.id,
    athlete_id: athlete.id,
    code_hash: await sha256(`${user.id}:${code}`),
    expires_at: new Date(Date.now() + LINK_MINUTES * 60_000).toISOString(),
    tries: 0,
  });
  if (error) throw error;
  await send(email, linkCodeEmail({ code, name: firstNameVocative(athlete.full_name), coach }));
  await logSent("link-code", email);
  return reply({ match: true, coach });
}

async function linkVerify(req: Request, body: Record<string, unknown>) {
  const user = await caller(req);
  if (!user) return fail("Χρειάζεται σύνδεση.", 401);
  const code = String(body.code ?? "").replace(/\D/g, "");

  const { data: row, error } = await db.from("athlete_link_codes")
    .select("athlete_id, code_hash, expires_at, tries").eq("auth_user_id", user.id).maybeSingle();
  if (error) throw error;
  if (!row || new Date(row.expires_at).getTime() < Date.now()) {
    return fail("Ο κωδικός έληξε. Ζήτα νέο.", 400, { expired: true, remaining: 0 });
  }
  if (row.tries >= LINK_TRIES) {
    return fail("Πάρα πολλές λάθος προσπάθειες. Ζήτα νέο κωδικό.", 400, { expired: true, remaining: 0 });
  }
  if (!same(await sha256(`${user.id}:${code}`), row.code_hash)) {
    const tries = row.tries + 1;
    await db.from("athlete_link_codes").update({ tries }).eq("auth_user_id", user.id);
    const remaining = LINK_TRIES - tries;
    return fail(
      remaining > 0 ? `Λάθος κωδικός. Απομένουν ${remaining} προσπάθειες.` : "Πάρα πολλές λάθος προσπάθειες. Ζήτα νέο κωδικό.",
      400, { remaining },
    );
  }

  // Link, only if still unclaimed and the emails still match.
  const { data: linked, error: linkError } = await db.from("athletes")
    .update({ auth_user_id: user.id })
    .eq("id", row.athlete_id).is("auth_user_id", null).ilike("email", (user.email ?? "").replace(/[%_\\]/g, "\\$&"))
    .select("id");
  if (linkError) throw linkError;
  await db.from("athlete_link_codes").delete().eq("auth_user_id", user.id);
  if (!linked?.length) return fail("Το προφίλ δεν είναι πια διαθέσιμο. Μίλησε με τον προπονητή σου.", 409);
  return reply({ linked: true });
}

async function invite(body: Record<string, unknown>) {
  const { data: a, error } = await db.from("athletes")
    .select("id, full_name, email, auth_user_id, invited_email, coaches(full_name)")
    .eq("id", String(body.athlete_id ?? "")).maybeSingle();
  if (error) throw error;
  if (!a?.email || a.auth_user_id) return reply({ sent: false });
  const email = String(a.email).toLowerCase();
  if (a.invited_email === email) return reply({ sent: false });

  // Someone who already has a login with this email gets it too: signing in, they link with a code.
  const coach = (a.coaches as { full_name: string } | null)?.full_name ?? "Ο προπονητής σου";
  if (await allowed("invite", email)) {
    await send(email, inviteEmail({ name: firstNameVocative(a.full_name), coach, email }));
    await logSent("invite", email);
  }
  await db.from("athletes").update({ invited_email: email }).eq("id", a.id);
  return reply({ sent: true });
}

async function newCoach(body: Record<string, unknown>) {
  const email = String(body.email ?? "").trim().toLowerCase();
  const password = String(body.password ?? "");
  if (!email || !password) return fail("email and password are required");
  await send(email, newCoachEmail({ name: firstNameVocative(String(body.full_name ?? "")), email, password }));
  await logSent("new-coach", email);
  return reply({ sent: true });
}

Deno.serve(async (req) => {
  if (req.method === "OPTIONS") return new Response("ok", { headers: cors });
  if (req.method !== "POST") return fail("POST only", 405);
  try {
    const body = await req.json().catch(() => ({})) as Record<string, unknown>;
    const bearer = (req.headers.get("Authorization") ?? "").replace(/^Bearer\s+/i, "");
    switch (body.action) {
      case "reset":
        return await reset(body);
      case "link-code":
        return await linkCode(req);
      case "link-verify":
        return await linkVerify(req, body);
      case "invite":
        if (!same(req.headers.get("x-email-hook") ?? "", HOOK_SECRET)) return fail("forbidden", 403);
        return await invite(body);
      case "new-coach":
        if (!same(bearer, SERVICE_KEY)) return fail("forbidden", 403);
        return await newCoach(body);
      default:
        return fail("unknown action");
    }
  } catch (e) {
    console.error(e);
    return fail("Κάτι πήγε στραβά. Δοκίμασε ξανά.", 500);
  }
});
