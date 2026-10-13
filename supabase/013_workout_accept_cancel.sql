-- AthloTrack migration 013: the athlete accepts or declines a new workout; the coach cancels one.
-- Run once in Supabase > SQL Editor (after 012). Safe to re-run.
--
-- A new workout first asks the athlete «Θα το κάνω» (accepted_at) or «Δεν θα το κάνω»
-- (declined_at, with an optional decline_reason). Either choice is once and final, and the coach is
-- notified. Only an accepted workout gets the final «Ολοκληρώθηκε / Δεν ολοκληρώθηκε» answer.
-- The coach can cancel a workout that has no final answer and wasn't declined (cancelled_at), and
-- restore it; the athlete is notified of both. Workouts from before this migration count as accepted.

-- Existing workouts keep working exactly as before: they count as accepted. Only when the column
-- is first added, so a re-run never accepts a workout on the athlete's behalf.
do $$
begin
  if not exists (select 1 from information_schema.columns
                 where table_schema = 'public' and table_name = 'workout_programs' and column_name = 'accepted_at') then
    alter table workout_programs add column accepted_at timestamptz;
    update workout_programs set accepted_at = created_at;
  end if;
end $$;
alter table workout_programs add column if not exists declined_at    timestamptz;
alter table workout_programs add column if not exists decline_reason text;
alter table workout_programs add column if not exists cancelled_at   timestamptz;

-- A new workout starts with no choice and isn't cancelled, whoever inserts it.
create or replace function guard_workout_insert() returns trigger
language plpgsql as $$
begin
  new.accepted_at      := null;
  new.declined_at      := null;
  new.decline_reason   := null;
  new.cancelled_at     := null;
  new.completed_at     := null;
  new.not_completed_at := null;
  new.last_reminded_at := null;
  return new;
end $$;

drop trigger if exists trg_guard_workout_insert on workout_programs;
create trigger trg_guard_workout_insert before insert on workout_programs
for each row execute function guard_workout_insert();

-- Same guard as 012, plus: the athlete's choice (accept / decline) comes first, once, and final;
-- the final answer needs an accepted workout; a cancelled workout takes no choice or answer.
-- Nobody else changes the athlete's fields; cancelled_at only changes through cancel_workout() /
-- restore_workout(), last_reminded_at only through remind_workout().
create or replace function guard_workout_athlete_edit() returns trigger
language plpgsql as $$
begin
  if old.athlete_id = auth_athlete_id() then
    new.athlete_id       := old.athlete_id;
    new.title            := old.title;
    new.content          := old.content;
    new.target_date      := old.target_date;
    new.coach_present    := old.coach_present;
    new.created_by       := old.created_by;
    new.created_at       := old.created_at;
    new.read_at          := coalesce(old.read_at, new.read_at);
    new.last_reminded_at := old.last_reminded_at;
    new.cancelled_at     := old.cancelled_at;

    -- The choice: once, final, not on a cancelled workout. «Θα το κάνω» wins if both arrive.
    if old.accepted_at is not null or old.declined_at is not null or old.cancelled_at is not null then
      new.accepted_at    := old.accepted_at;
      new.declined_at    := old.declined_at;
      new.decline_reason := old.decline_reason;
    elsif new.accepted_at is not null then
      new.declined_at    := null;
      new.decline_reason := null;
    elsif new.declined_at is not null then
      new.decline_reason := nullif(left(btrim(coalesce(new.decline_reason, '')), 200), '');
    else
      new.decline_reason := null;
    end if;

    -- The final answer: once, and only on an accepted, open workout.
    if old.completed_at is not null or old.not_completed_at is not null
       or new.accepted_at is null or new.declined_at is not null or old.cancelled_at is not null then
      new.completed_at     := old.completed_at;
      new.not_completed_at := old.not_completed_at;
    elsif new.completed_at is not null then
      new.not_completed_at := null;
    end if;
  else
    -- The coach (or anyone else): the choice and the answer are the athlete's.
    new.accepted_at      := old.accepted_at;
    new.declined_at      := old.declined_at;
    new.decline_reason   := old.decline_reason;
    new.completed_at     := old.completed_at;
    new.not_completed_at := old.not_completed_at;
    if coalesce(current_setting('athlotrack.reminding', true), '') <> '1' then
      new.last_reminded_at := old.last_reminded_at;
    end if;
    if coalesce(current_setting('athlotrack.cancelling', true), '') <> '1' then
      new.cancelled_at := old.cancelled_at;
    end if;

    if new.content is distinct from old.content
       or new.target_date is distinct from old.target_date
       or new.coach_present is distinct from old.coach_present then
      new.read_at := null;
    else
      new.read_at := old.read_at;
    end if;
  end if;
  return new;
end $$;

-- The athlete chose -> the coach is notified.
create or replace function notify_workout_choice() returns trigger
language plpgsql security definer set search_path = public as $$
begin
  if old.accepted_at is null and new.accepted_at is not null then
    insert into notifications (athlete_id, recipient, type, message, related_workout_id)
    select new.athlete_id, 'coach', 'workout_accepted',
           'Ο/Η ' || a.full_name || ' θα κάνει το ασκησιολόγιο της ' || to_char(new.target_date, 'DD/MM'),
           new.id
    from athletes a where a.id = new.athlete_id;
  elsif old.declined_at is null and new.declined_at is not null then
    insert into notifications (athlete_id, recipient, type, message, related_workout_id)
    select new.athlete_id, 'coach', 'workout_declined',
           'Ο/Η ' || a.full_name || ' δεν θα κάνει το ασκησιολόγιο της ' || to_char(new.target_date, 'DD/MM')
             || coalesce(': ' || new.decline_reason, ''),
           new.id
    from athletes a where a.id = new.athlete_id;
  end if;
  return new;
end $$;

drop trigger if exists trg_notify_workout_choice on workout_programs;
create trigger trg_notify_workout_choice after update of accepted_at, declined_at on workout_programs
for each row execute function notify_workout_choice();

-- The coach cancels a workout: only the athlete's own coach, only while it has no final answer
-- and wasn't declined. Cancelling twice changes nothing.
create or replace function cancel_workout(p_workout uuid) returns void
language plpgsql security definer set search_path = public as $$
declare
  w workout_programs;
  v_coach uuid := auth_coach_id();
  v_coach_name text;
begin
  select wp.* into w
  from workout_programs wp join athletes a on a.id = wp.athlete_id
  where wp.id = p_workout and a.coach_id = v_coach;
  if v_coach is null or w.id is null then
    raise exception 'not allowed' using errcode = '42501';
  end if;
  if w.cancelled_at is not null then return; end if;
  if w.completed_at is not null or w.not_completed_at is not null or w.declined_at is not null then
    raise exception 'already answered';
  end if;

  perform set_config('athlotrack.cancelling', '1', true);
  update workout_programs set cancelled_at = now() where id = p_workout;
  perform set_config('athlotrack.cancelling', '', true);

  select full_name into v_coach_name from coaches where id = v_coach;
  insert into notifications (athlete_id, recipient, type, message, related_workout_id)
  values (w.athlete_id, 'athlete', 'workout_cancelled',
          'Ο ' || v_coach_name || ' ακύρωσε το ασκησιολόγιο της ' || to_char(w.target_date, 'DD/MM'),
          w.id);
end $$;

-- The coach undoes a cancel: the workout is as it was before (waiting for a choice, or accepted).
create or replace function restore_workout(p_workout uuid) returns void
language plpgsql security definer set search_path = public as $$
declare
  w workout_programs;
  v_coach uuid := auth_coach_id();
  v_coach_name text;
begin
  select wp.* into w
  from workout_programs wp join athletes a on a.id = wp.athlete_id
  where wp.id = p_workout and a.coach_id = v_coach;
  if v_coach is null or w.id is null then
    raise exception 'not allowed' using errcode = '42501';
  end if;
  if w.cancelled_at is null then return; end if;

  perform set_config('athlotrack.cancelling', '1', true);
  update workout_programs set cancelled_at = null where id = p_workout;
  perform set_config('athlotrack.cancelling', '', true);

  select full_name into v_coach_name from coaches where id = v_coach;
  insert into notifications (athlete_id, recipient, type, message, related_workout_id)
  values (w.athlete_id, 'athlete', 'workout_restored',
          'Ο ' || v_coach_name || ' επανέφερε το ασκησιολόγιο της ' || to_char(w.target_date, 'DD/MM'),
          w.id);
end $$;

-- 012's reminder, plus: not on a cancelled or declined workout; before the athlete has chosen,
-- it asks for the choice instead.
create or replace function remind_workout(p_workout uuid) returns void
language plpgsql security definer set search_path = public as $$
declare
  w workout_programs;
  v_coach uuid := auth_coach_id();
  v_coach_name text;
begin
  select wp.* into w
  from workout_programs wp join athletes a on a.id = wp.athlete_id
  where wp.id = p_workout and a.coach_id = v_coach;
  if v_coach is null or w.id is null then
    raise exception 'not allowed' using errcode = '42501';
  end if;
  if w.completed_at is not null or w.not_completed_at is not null
     or w.declined_at is not null or w.cancelled_at is not null then
    raise exception 'already answered';
  end if;
  if w.last_reminded_at is not null and w.last_reminded_at > now() - interval '1 hour' then
    raise exception 'too soon';
  end if;

  perform set_config('athlotrack.reminding', '1', true); -- lets the guard keep the new time
  update workout_programs set last_reminded_at = now() where id = p_workout;
  perform set_config('athlotrack.reminding', '', true);

  select full_name into v_coach_name from coaches where id = v_coach;
  insert into notifications (athlete_id, recipient, type, message, related_workout_id)
  values (w.athlete_id, 'athlete', 'workout_reminder',
          case when w.accepted_at is null
               then 'Ο ' || v_coach_name || ' περιμένει να απαντήσεις αν θα κάνεις το ασκησιολόγιο της ' || to_char(w.target_date, 'DD/MM')
               else 'Ο ' || v_coach_name || ' ρωτά: ολοκλήρωσες το ασκησιολόγιο της ' || to_char(w.target_date, 'DD/MM') || ';'
          end,
          w.id);
end $$;

-- Admin overview (011): cancelled and declined workouts aren't due.
create or replace function admin_overview() returns jsonb
language plpgsql stable security definer set search_path = public as $$
begin
  perform admin_guard();
  return jsonb_build_object(
    'coaches',            (select count(*) from coaches),
    'athletes',           (select count(*) from athletes),
    'linked_athletes',    (select count(*) from athletes where auth_user_id is not null),
    'logins',             (select count(*) from auth.users),
    'active_7d',          (select count(*) from auth.users where last_sign_in_at >= now() - interval '7 days'),
    'active_30d',         (select count(*) from auth.users where last_sign_in_at >= now() - interval '30 days'),
    'app_users',          (select count(distinct auth_user_id) from device_tokens),
    'workouts_total',     (select count(*) from workout_programs),
    'workouts_week',      (select count(*) from workout_programs where created_at >= date_trunc('week', now())),
    'workouts_month',     (select count(*) from workout_programs where created_at >= date_trunc('month', now())),
    'due_30d',            (select count(*) from workout_programs
                            where target_date between current_date - 30 and current_date
                              and cancelled_at is null and declined_at is null),
    'completed_30d',      (select count(*) from workout_programs
                            where target_date between current_date - 30 and current_date and completed_at is not null),
    'read_30d',           (select count(*) from workout_programs
                            where target_date between current_date - 30 and current_date and read_at is not null
                              and cancelled_at is null and declined_at is null),
    'measurements_month', (select count(*) from measurements where created_at >= date_trunc('month', now()))
  );
end $$;
