-- AthloTrack migration 022: email service (Edge Function "email") and linking with a code.
-- Run once in Supabase > SQL Editor (after 021), AFTER the "email" function is deployed and the
-- vault secret below exists. Safe to re-run.
--
-- 1. Logins no longer link to an athlete profile just because the emails match (anyone could sign
--    up with an athlete's email and see their data). Linking now needs the 6-digit code the
--    "email" function sends to that address (actions link-code / link-verify). Accounts that are
--    already linked stay linked.
-- 2. email_log: what was sent and when (rate limits, debugging). athlete_link_codes: pending codes
--    (hashed). Neither is visible to the app: only the function (service role) uses them.
-- 3. A coach adds an athlete's email (no login yet): the function sends an invite, once per email
--    (athletes.invited_email). The trigger calls the function through pg_net with the secret kept
--    in Vault as 'email_hook_secret' (the same value as the function's EMAIL_HOOK_SECRET).

-- ---------------------------------------------------------------------------
-- 1. No more automatic linking
-- ---------------------------------------------------------------------------
drop trigger if exists trg_link_athlete_on_signup on auth.users;
drop trigger if exists trg_link_athlete_on_email on athletes;
drop function if exists link_athlete_on_signup();
drop function if exists link_athlete_on_email();

-- ---------------------------------------------------------------------------
-- 2. Tables only the function uses
-- ---------------------------------------------------------------------------
create table if not exists email_log (
  id         bigint generated always as identity primary key,
  kind       text not null,          -- reset | link-code | invite | new-coach
  email      text not null,          -- lower-cased
  created_at timestamptz not null default now()
);
create index if not exists idx_email_log_recent on email_log (email, kind, created_at desc);
alter table email_log enable row level security; -- no policies: no client access

create table if not exists athlete_link_codes (
  auth_user_id uuid primary key references auth.users (id) on delete cascade,
  athlete_id   uuid not null references athletes (id) on delete cascade,
  code_hash    text not null,
  expires_at   timestamptz not null,
  tries        int not null default 0,
  created_at   timestamptz not null default now()
);
alter table athlete_link_codes enable row level security; -- no policies: no client access

-- ---------------------------------------------------------------------------
-- 3. Invites
-- ---------------------------------------------------------------------------
alter table athletes add column if not exists invited_email text;
-- Athletes already added before 022 are not invited after the fact.
update athletes set invited_email = lower(email)
 where email is not null and invited_email is null;

-- The athlete self-edit guard keeps invited_email too.
create or replace function guard_athlete_self_edit() returns trigger
language plpgsql as $$
begin
  if old.auth_user_id = auth.uid() then
    new.coach_id      := old.coach_id;
    new.auth_user_id  := old.auth_user_id;
    new.email         := old.email;
    new.notes         := old.notes;
    new.created_at    := old.created_at;
    new.invited_email := old.invited_email;
  end if;
  return new;
end $$;

create extension if not exists pg_net with schema extensions;

create or replace function queue_athlete_invite() returns trigger
language plpgsql security definer set search_path = public, extensions as $$
declare
  v_secret text;
begin
  select decrypted_secret into v_secret from vault.decrypted_secrets where name = 'email_hook_secret';
  if v_secret is null then
    raise warning 'email_hook_secret missing in Vault: no invite sent';
    return new;
  end if;
  perform net.http_post(
    url     := 'https://tahsfrptizcdqhvghzqs.supabase.co/functions/v1/email',
    headers := jsonb_build_object('Content-Type', 'application/json', 'x-email-hook', v_secret),
    body    := jsonb_build_object('action', 'invite', 'athlete_id', new.id)
  );
  return new;
end $$;

revoke execute on function queue_athlete_invite() from public, anon, authenticated;

drop trigger if exists trg_queue_athlete_invite on athletes;
create trigger trg_queue_athlete_invite
after insert or update of email on athletes
for each row
when (new.email is not null and new.auth_user_id is null
      and new.invited_email is distinct from lower(new.email))
execute function queue_athlete_invite();
