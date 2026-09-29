-- AthloTrack migration 002: athlete accounts linked by email, self-service athlete edits,
-- coach photos. Run once in Supabase > SQL Editor (after schema.sql). Safe to re-run.

-- ---------------------------------------------------------------------------
-- 1. Columns
-- ---------------------------------------------------------------------------
alter table athletes add column if not exists email text;
create unique index if not exists idx_athletes_email on athletes (lower(email)) where email is not null;

alter table coaches add column if not exists profile_image_path text;

-- ---------------------------------------------------------------------------
-- 2. Helpers (security definer: lets policies look across tables without RLS recursion)
-- ---------------------------------------------------------------------------
create or replace function auth_coach_id() returns uuid
language sql stable security definer set search_path = public as $$
  select id from coaches where auth_user_id = auth.uid()
$$;

create or replace function auth_athlete_id() returns uuid
language sql stable security definer set search_path = public as $$
  select id from athletes where auth_user_id = auth.uid()
$$;

create or replace function auth_athlete_coach_id() returns uuid
language sql stable security definer set search_path = public as $$
  select coach_id from athletes where auth_user_id = auth.uid()
$$;

-- ---------------------------------------------------------------------------
-- 3. Linking by email
-- ---------------------------------------------------------------------------
-- A new login whose email matches an athlete becomes that athlete's account.
create or replace function link_athlete_on_signup() returns trigger
language plpgsql security definer set search_path = public as $$
begin
  update athletes set auth_user_id = new.id
  where auth_user_id is null and email is not null and lower(email) = lower(new.email);
  return new;
end $$;

drop trigger if exists trg_link_athlete_on_signup on auth.users;
create trigger trg_link_athlete_on_signup after insert on auth.users
for each row execute function link_athlete_on_signup();

-- The coach sets/changes an athlete's email: link to an existing login with that email.
create or replace function link_athlete_on_email() returns trigger
language plpgsql security definer set search_path = public as $$
begin
  if new.email is not null and new.auth_user_id is null then
    select u.id into new.auth_user_id from auth.users u
    where lower(u.email) = lower(new.email)
      and not exists (select 1 from athletes a where a.auth_user_id = u.id and a.id <> new.id)
    limit 1;
  end if;
  return new;
end $$;

drop trigger if exists trg_link_athlete_on_email on athletes;
create trigger trg_link_athlete_on_email before insert or update of email on athletes
for each row execute function link_athlete_on_email();

-- Link any logins that already exist (e.g. created by hand in the dashboard).
update athletes a set auth_user_id = u.id
from auth.users u
where a.auth_user_id is null and a.email is not null and lower(a.email) = lower(u.email);

-- ---------------------------------------------------------------------------
-- 4. Athletes may edit their own photo, name, date of birth, height — nothing else
-- ---------------------------------------------------------------------------
drop policy if exists athlete_update_own_row on athletes;
create policy athlete_update_own_row on athletes
  for update using (auth_user_id = auth.uid()) with check (auth_user_id = auth.uid());

create or replace function guard_athlete_self_edit() returns trigger
language plpgsql as $$
begin
  if old.auth_user_id = auth.uid() then
    new.coach_id     := old.coach_id;
    new.auth_user_id := old.auth_user_id;
    new.email        := old.email;
    new.notes        := old.notes;
    new.created_at   := old.created_at;
  end if;
  return new;
end $$;

drop trigger if exists trg_guard_athlete_self_edit on athletes;
create trigger trg_guard_athlete_self_edit before update on athletes
for each row execute function guard_athlete_self_edit();

-- ---------------------------------------------------------------------------
-- 5. Athletes can see their coach (name + photo)
-- ---------------------------------------------------------------------------
drop policy if exists athlete_read_own_coach on coaches;
create policy athlete_read_own_coach on coaches
  for select using (id = auth_athlete_coach_id());

-- ---------------------------------------------------------------------------
-- 6. Storage: avatars/{athlete_id}/... and avatars/{coach_id}/...
-- ---------------------------------------------------------------------------
drop policy if exists athlete_read_own_avatar on storage.objects;
drop policy if exists athlete_manage_own_avatar on storage.objects;
create policy athlete_manage_own_avatar on storage.objects
  for all using (bucket_id = 'avatars' and (storage.foldername(name))[1] = auth_athlete_id()::text)
  with check (bucket_id = 'avatars' and (storage.foldername(name))[1] = auth_athlete_id()::text);

drop policy if exists coach_manage_own_avatar on storage.objects;
create policy coach_manage_own_avatar on storage.objects
  for all using (bucket_id = 'avatars' and (storage.foldername(name))[1] = auth_coach_id()::text)
  with check (bucket_id = 'avatars' and (storage.foldername(name))[1] = auth_coach_id()::text);

drop policy if exists athlete_read_coach_avatar on storage.objects;
create policy athlete_read_coach_avatar on storage.objects
  for select using (bucket_id = 'avatars' and (storage.foldername(name))[1] = auth_athlete_coach_id()::text);

-- The original coach policy cast the folder to uuid; compare as text so non-uuid names can't error.
drop policy if exists coach_manage_avatar_files on storage.objects;
create policy coach_manage_avatar_files on storage.objects
  for all using (
    bucket_id = 'avatars'
    and (storage.foldername(name))[1] in (select a.id::text from athletes a where a.coach_id = auth_coach_id())
  )
  with check (
    bucket_id = 'avatars'
    and (storage.foldername(name))[1] in (select a.id::text from athletes a where a.coach_id = auth_coach_id())
  );

-- The bucket itself (no-op if it already exists).
insert into storage.buckets (id, name, public) values ('avatars', 'avatars', false)
on conflict (id) do nothing;
