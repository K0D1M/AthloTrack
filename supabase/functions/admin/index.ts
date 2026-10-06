// AthloTrack admin actions (Supabase Edge Function, Deno), called by the app's «Διαχείριση».
// Only for logins in public.admins: the caller's JWT is checked on every request. Everything here
// needs the auth admin API or the service role, which must never be in the app.
//
// Actions (POST JSON { action, ... }):
//   create_coach   { email, full_name }      -> { temp_password }  login + coaches row (must set password)
//   reset_password { auth_user_id }          -> { temp_password }  coaches also must set a new one
//   delete_login   { auth_user_id }          -> {}                 cascades (a coach takes their athletes)
//   test_push      { auth_user_id }          -> { sent }
//   broadcast      { audience, title, body } -> { sent, users }    audience: coaches | athletes | all
// Every action is written to public.admin_audit.
//
// Provided by Supabase automatically: SUPABASE_URL, SUPABASE_SERVICE_ROLE_KEY

import { createClient } from "npm:@supabase/supabase-js@2";

const SUPABASE_URL = Deno.env.get("SUPABASE_URL")!;
const SERVICE_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
const supabase = createClient(SUPABASE_URL, SERVICE_KEY, { auth: { persistSession: false } });

const cors = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, apikey, content-type, x-client-info",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};
const reply = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { ...cors, "Content-Type": "application/json" } });
const fail = (message: string, status = 400) => reply({ error: message }, status);

// A readable temporary password (no 0/O/1/l): 12 characters, the coach replaces it at sign-in.
function tempPassword(): string {
  const chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
  const bytes = crypto.getRandomValues(new Uint8Array(12));
  return Array.from(bytes, (b) => chars[b % chars.length]).join("");
}

async function audit(adminId: string, action: string, target: string, details: Record<string, unknown>) {
  const { error } = await supabase.from("admin_audit")
    .insert({ admin_auth_id: adminId, action, target, details });
  if (error) console.error("audit", error);
}

async function push(userIds: string[], title: string, body: string): Promise<number> {
  const res = await fetch(`${SUPABASE_URL}/functions/v1/push`, {
    method: "POST",
    headers: { Authorization: `Bearer ${SERVICE_KEY}`, "Content-Type": "application/json" },
    body: JSON.stringify({ direct: { auth_user_ids: userIds, title, body } }),
  });
  if (!res.ok) throw new Error(`push ${res.status}: ${await res.text()}`);
  return (await res.json()).sent ?? 0;
}

async function emailOf(userId: string): Promise<string> {
  const { data } = await supabase.auth.admin.getUserById(userId);
  return data.user?.email ?? userId;
}

Deno.serve(async (req) => {
  if (req.method === "OPTIONS") return new Response("ok", { headers: cors });
  try {
    // ---- Who is calling: must be an admin ----
    const jwt = (req.headers.get("Authorization") ?? "").replace(/^Bearer\s+/i, "");
    const { data: caller } = await supabase.auth.getUser(jwt);
    const adminId = caller.user?.id;
    if (!adminId) return fail("not signed in", 401);
    const { data: admin } = await supabase.from("admins").select("id").eq("auth_user_id", adminId).maybeSingle();
    if (!admin) return fail("not admin", 403);

    const p = await req.json();
    switch (p.action) {
      case "create_coach": {
        const email = String(p.email ?? "").trim().toLowerCase();
        const fullName = String(p.full_name ?? "").trim();
        if (!email.includes("@") || !fullName) return fail("email and full_name are required");

        const password = tempPassword();
        const { data: created, error } = await supabase.auth.admin.createUser({
          email, password, email_confirm: true,
        });
        if (error || !created.user) return fail(error?.message ?? "could not create the login");

        const { error: rowError } = await supabase.from("coaches")
          .insert({ auth_user_id: created.user.id, full_name: fullName, email, must_set_password: true });
        if (rowError) {
          await supabase.auth.admin.deleteUser(created.user.id); // don't leave a login without a profile
          return fail(rowError.message);
        }
        await audit(adminId, "create_coach", fullName, { email, auth_user_id: created.user.id });
        return reply({ temp_password: password, auth_user_id: created.user.id });
      }

      case "reset_password": {
        const userId = String(p.auth_user_id ?? "");
        if (!userId) return fail("auth_user_id is required");
        const password = tempPassword();
        const { error } = await supabase.auth.admin.updateUserById(userId, { password });
        if (error) return fail(error.message);
        // A coach is asked for their own password at the next sign-in.
        await supabase.from("coaches").update({ must_set_password: true }).eq("auth_user_id", userId);
        await audit(adminId, "reset_password", await emailOf(userId), { auth_user_id: userId });
        return reply({ temp_password: password });
      }

      case "delete_login": {
        const userId = String(p.auth_user_id ?? "");
        if (!userId) return fail("auth_user_id is required");
        if (userId === adminId) return fail("you can't delete your own login");
        const email = await emailOf(userId);
        const { error } = await supabase.auth.admin.deleteUser(userId);
        if (error) return fail(error.message);
        await audit(adminId, "delete_login", email, { auth_user_id: userId });
        return reply({});
      }

      case "test_push": {
        const userId = String(p.auth_user_id ?? "");
        if (!userId) return fail("auth_user_id is required");
        const sent = await push([userId], "AthloTrack", "Δοκιμαστική ειδοποίηση από τη διαχείριση.");
        await audit(adminId, "test_push", await emailOf(userId), { auth_user_id: userId, sent });
        return reply({ sent });
      }

      case "broadcast": {
        const audience = String(p.audience ?? "");
        const body = String(p.body ?? "").trim();
        const title = String(p.title ?? "").trim() || "AthloTrack";
        if (!body) return fail("body is required");
        if (!["coaches", "athletes", "all"].includes(audience)) return fail("audience: coaches | athletes | all");

        const ids: string[] = [];
        if (audience !== "athletes") {
          const { data } = await supabase.from("coaches").select("auth_user_id").not("auth_user_id", "is", null);
          ids.push(...(data ?? []).map((r) => r.auth_user_id));
        }
        if (audience !== "coaches") {
          const { data } = await supabase.from("athletes").select("auth_user_id").not("auth_user_id", "is", null);
          ids.push(...(data ?? []).map((r) => r.auth_user_id));
        }
        const users = [...new Set(ids)];
        const sent = await push(users, title, body);
        await audit(adminId, "broadcast", audience, { title, body, users: users.length, sent });
        return reply({ sent, users: users.length });
      }

      default:
        return fail(`unknown action: ${p.action}`);
    }
  } catch (e) {
    console.error(e);
    return fail(String(e), 500);
  }
});
