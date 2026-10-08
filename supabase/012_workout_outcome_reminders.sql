-- AthloTrack migration 012: «Δεν ολοκληρώθηκε», a final answer, and coach reminders.
-- Run once in Supabase > SQL Editor (after 011). Safe to re-run.
--
-- An athlete answers a workout once: «Ολοκληρώθηκε» (completed_at) or «Δεν ολοκληρώθηκε»
-- (not_completed_at). Either answer is final. The coach is notified of both. While a workout has
-- no answer, the coach can send a reminder (at most once an hour), which notifies the athlete.

alter table workout_programs add column if not exists not_completed_at timestamptz;
alter table workout_programs add column if not exists last_reminded_at timestamptz;

-- Same guard as 008, plus: the athlete's answer is one of the two, once, and final. Nobody else
-- changes the answer, and last_reminded_at only changes through remind_workout().
create or replace function guard_workout_athlete_edit() returns trigger
language plpgsql as $$
begin
  if old.athlete_id = auth_athlete_id() then
    new.athlete_id    := old.athlete_id;
    new.title         := old.title;
    new.content       := old.content;
    new.target_date   := old.target_date;
    new.coach_present := old.coach_present;
    new.created_by    := old.created_by;
    new.created_at    := old.created_at;
    new.read_at       := coalesce(old.read_at, new.read_at);
    new.last_reminded_at := old.last_reminded_at;

    if old.completed_at is not null or old.not_completed_at is not null then
      -- Already answered: final.
      new.completed_at     := old.completed_at;
      new.not_completed_at := old.not_completed_at;
    elsif new.completed_at is not null then
      -- One answer only: «Ολοκληρώθηκε» wins if both arrive.
      new.not_completed_at := null;
    end if;
  else
    -- The coach (or anyone else): the answer is the athlete's.
    new.completed_at     := old.completed_at;
    new.not_completed_at := old.not_completed_at;
    if coalesce(current_setting('athlotrack.reminding', true), '') <> '1' then
      new.last_reminded_at := old.last_reminded_at;
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

-- Athlete answers «Δεν ολοκληρώθηκε» -> the coach is notified.
create or replace function notify_workout_not_completed() returns trigger
language plpgsql security definer set search_path = public as $$
begin
  if old.not_completed_at is null and new.not_completed_at is not null then
    insert into notifications (athlete_id, recipient, type, message, related_workout_id)
    select new.athlete_id, 'coach', 'workout_not_completed',
           'Ο/Η ' || a.full_name || ' δεν ολοκλήρωσε το ασκησιολόγιο της ' || to_char(new.target_date, 'DD/MM'),
           new.id
    from athletes a where a.id = new.athlete_id;
  end if;
  return new;
end $$;

drop trigger if exists trg_notify_workout_not_completed on workout_programs;
create trigger trg_notify_workout_not_completed after update of not_completed_at on workout_programs
for each row execute function notify_workout_not_completed();

-- The coach asks the athlete whether a workout was done. Only the athlete's own coach, only
-- while it has no answer, and at most once an hour per workout.
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
  if w.completed_at is not null or w.not_completed_at is not null then
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
          'Ο ' || v_coach_name || ' ρωτά: ολοκλήρωσες το ασκησιολόγιο της ' || to_char(w.target_date, 'DD/MM') || ';',
          w.id);
end $$;
