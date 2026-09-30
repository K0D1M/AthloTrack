-- AthloTrack migration 005: athletes mark workouts done; coaches get notified; phone push tokens.
-- Run once in Supabase > SQL Editor (after 004). Safe to re-run.

-- ---------------------------------------------------------------------------
-- 1. Workout completion
-- ---------------------------------------------------------------------------
alter table workout_programs add column if not exists completed_at timestamptz;

-- The athlete may update their own workouts ...
drop policy if exists athlete_complete_own_workouts on workout_programs;
create policy athlete_complete_own_workouts on workout_programs
  for update using (athlete_id = auth_athlete_id()) with check (athlete_id = auth_athlete_id());

-- ... but only the completion timestamp.
create or replace function guard_workout_athlete_edit() returns trigger
language plpgsql as $$
begin
  if old.athlete_id = auth_athlete_id() then
    new.athlete_id  := old.athlete_id;
    new.title       := old.title;
    new.content     := old.content;
    new.target_date := old.target_date;
    new.created_by  := old.created_by;
    new.created_at  := old.created_at;
  end if;
  return new;
end $$;

drop trigger if exists trg_guard_workout_athlete_edit on workout_programs;
create trigger trg_guard_workout_athlete_edit before update on workout_programs
for each row execute function guard_workout_athlete_edit();

-- ---------------------------------------------------------------------------
-- 2. Notifications for coaches too
-- ---------------------------------------------------------------------------
alter table notifications add column if not exists recipient text not null default 'athlete';
alter table notifications drop constraint if exists notifications_recipient_check;
alter table notifications add constraint notifications_recipient_check check (recipient in ('athlete', 'coach'));

-- Athletes only see (and mark read) notifications addressed to them.
drop policy if exists athlete_read_own_notifications on notifications;
create policy athlete_read_own_notifications on notifications
  for select using (recipient = 'athlete' and athlete_id = auth_athlete_id());

drop policy if exists athlete_update_own_notifications on notifications;
create policy athlete_update_own_notifications on notifications
  for update using (recipient = 'athlete' and athlete_id = auth_athlete_id());

-- Athlete marks a workout done -> the coach is notified.
create or replace function notify_workout_completed() returns trigger
language plpgsql security definer set search_path = public as $$
begin
  if old.completed_at is null and new.completed_at is not null then
    insert into notifications (athlete_id, recipient, type, message, related_workout_id)
    select new.athlete_id, 'coach', 'workout_completed',
           'Ο/Η ' || a.full_name || ' ολοκλήρωσε το ασκησιολόγιο της ' || to_char(new.target_date, 'DD/MM'),
           new.id
    from athletes a where a.id = new.athlete_id;
  end if;
  return new;
end $$;

drop trigger if exists trg_notify_workout_completed on workout_programs;
create trigger trg_notify_workout_completed after update of completed_at on workout_programs
for each row execute function notify_workout_completed();

-- ---------------------------------------------------------------------------
-- 3. Phone push tokens (one row per installed app; each user manages their own)
-- ---------------------------------------------------------------------------
create table if not exists device_tokens (
  token        text primary key,
  auth_user_id uuid not null default auth.uid() references auth.users(id) on delete cascade,
  platform     text not null default 'android',
  updated_at   timestamptz not null default now()
);
alter table device_tokens enable row level security;

drop policy if exists own_device_tokens on device_tokens;
create policy own_device_tokens on device_tokens
  for all using (auth_user_id = auth.uid()) with check (auth_user_id = auth.uid());

-- A token moves with whoever signs in on that phone: let the new user take it over.
create or replace function claim_device_token(p_token text, p_platform text default 'android')
returns void language sql security definer set search_path = public as $$
  insert into device_tokens (token, auth_user_id, platform, updated_at)
  values (p_token, auth.uid(), p_platform, now())
  on conflict (token) do update set auth_user_id = auth.uid(), platform = excluded.platform, updated_at = now();
$$;
grant execute on function claim_device_token(text, text) to authenticated;
