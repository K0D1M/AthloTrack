// AthloTrack push sender (Supabase Edge Function, Deno).
// Triggered by a Database Webhook on INSERT into public.notifications. Sends the notification
// as an FCM push to every registered Android device of the recipient (athlete or coach).
//
// Secrets (Edge Functions > Secrets):
//   FIREBASE_SERVICE_ACCOUNT  the Firebase service-account JSON (Project settings > Service accounts)
// Provided by Supabase automatically: SUPABASE_URL, SUPABASE_SERVICE_ROLE_KEY

import { createClient } from "npm:@supabase/supabase-js@2";

type NotificationRow = {
  id: string;
  athlete_id: string;
  recipient: "athlete" | "coach";
  message: string;
  type: string;
  related_workout_id: string | null;
};

const supabase = createClient(
  Deno.env.get("SUPABASE_URL")!,
  Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!,
);
const serviceAccount = JSON.parse(Deno.env.get("FIREBASE_SERVICE_ACCOUNT") ?? "{}");

// ---- Google OAuth access token from the service account (JWT bearer flow, RS256) ----
let cachedToken: { value: string; expires: number } | null = null;

function base64url(data: ArrayBuffer | string): string {
  const bytes = typeof data === "string" ? new TextEncoder().encode(data) : new Uint8Array(data);
  let bin = "";
  for (const b of bytes) bin += String.fromCharCode(b);
  return btoa(bin).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

async function googleAccessToken(): Promise<string> {
  const now = Math.floor(Date.now() / 1000);
  if (cachedToken && cachedToken.expires > now + 60) return cachedToken.value;

  const header = base64url(JSON.stringify({ alg: "RS256", typ: "JWT" }));
  const claims = base64url(JSON.stringify({
    iss: serviceAccount.client_email,
    scope: "https://www.googleapis.com/auth/firebase.messaging",
    aud: "https://oauth2.googleapis.com/token",
    iat: now,
    exp: now + 3600,
  }));

  const pem = (serviceAccount.private_key as string)
    .replace(/-----[^-]+-----/g, "")
    .replace(/\s+/g, "");
  const der = Uint8Array.from(atob(pem), (c) => c.charCodeAt(0));
  const key = await crypto.subtle.importKey(
    "pkcs8", der, { name: "RSASSA-PKCS1-v1_5", hash: "SHA-256" }, false, ["sign"]);
  const signature = await crypto.subtle.sign(
    "RSASSA-PKCS1-v1_5", key, new TextEncoder().encode(`${header}.${claims}`));
  const jwt = `${header}.${claims}.${base64url(signature)}`;

  const res = await fetch("https://oauth2.googleapis.com/token", {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({ grant_type: "urn:ietf:params:oauth:grant-type:jwt-bearer", assertion: jwt }),
  });
  const json = await res.json();
  if (!res.ok) throw new Error(`Google token error: ${JSON.stringify(json)}`);
  cachedToken = { value: json.access_token, expires: now + (json.expires_in ?? 3600) };
  return cachedToken.value;
}

// ---- Who receives it ----
async function recipientAuthUserId(n: NotificationRow): Promise<string | null> {
  const { data: athlete } = await supabase
    .from("athletes").select("auth_user_id, coach_id").eq("id", n.athlete_id).single();
  if (!athlete) return null;
  if (n.recipient === "athlete") return athlete.auth_user_id;

  const { data: coach } = await supabase
    .from("coaches").select("auth_user_id").eq("id", athlete.coach_id).single();
  return coach?.auth_user_id ?? null;
}

Deno.serve(async (req) => {
  try {
    const payload = await req.json();
    const n = payload.record as NotificationRow | undefined;
    if (!n?.id) return new Response("no record", { status: 400 });

    const userId = await recipientAuthUserId(n);
    if (!userId) return Response.json({ sent: 0, reason: "recipient has no login" });

    const { data: tokens } = await supabase
      .from("device_tokens").select("token").eq("auth_user_id", userId);
    if (!tokens?.length) return Response.json({ sent: 0, reason: "no devices" });

    const access = await googleAccessToken();
    const url = `https://fcm.googleapis.com/v1/projects/${serviceAccount.project_id}/messages:send`;
    let sent = 0;

    for (const { token } of tokens) {
      const res = await fetch(url, {
        method: "POST",
        headers: { Authorization: `Bearer ${access}`, "Content-Type": "application/json" },
        body: JSON.stringify({
          message: {
            token,
            notification: { title: "AthloTrack", body: n.message },
            data: { notificationId: n.id, type: n.type },
            android: { priority: "high", notification: { channel_id: "athlotrack" } },
          },
        }),
      });
      if (res.ok) {
        sent++;
      } else {
        const err = await res.text();
        // App uninstalled or token rotated: forget it.
        if (res.status === 404 || err.includes("UNREGISTERED") || err.includes("INVALID_ARGUMENT")) {
          await supabase.from("device_tokens").delete().eq("token", token);
        }
        console.error("FCM error", res.status, err);
      }
    }
    return Response.json({ sent });
  } catch (e) {
    console.error(e);
    return new Response(String(e), { status: 500 });
  }
});
