-- AthloTrack migration 014: an optional start time for a workout («Ώρα»).
-- Run once in Supabase > SQL Editor (after 013). Safe to re-run.
--
-- start_time is the time of day on target_date (null = no time, as before). Only the coach sets
-- it; changing it counts as an edit like the date: the read receipt is cleared and the athlete is
-- notified («… ενημέρωσε το ασκησιολόγιο …»).

alter table workout_programs add column if not exists start_time time;

-- 013's guard, plus start_time: the athlete can't change it; a coach's change clears read_at.
create or replace function guard_workout_athlete_edit() returns trigger
language plpgsql as $$
begin
  if old.athlete_id = auth_athlete_id() then
    new.athlete_id       := old.athlete_id;
    new.title            := old.title;
    new.content          := old.content;
    new.target_date      := old.target_date;
    new.start_time       := old.start_time;
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
       or new.start_time is distinct from old.start_time
       or new.coach_present is distinct from old.coach_present then
      new.read_at := null;
    else
      new.read_at := old.read_at;
    end if;
  end if;
  return new;
end $$;

-- 007's edit notification, plus a change of start_time.
create or replace function notify_workout_updated() returns trigger
language plpgsql security definer set search_path = public as $$
begin
  if auth_coach_id() is not null
     and (new.content is distinct from old.content
          or new.target_date is distinct from old.target_date
          or new.start_time is distinct from old.start_time) then
    insert into notifications (athlete_id, recipient, type, message, related_workout_id)
    select new.athlete_id, 'athlete', 'workout_updated',
           'Ο ' || c.full_name || ' ενημέρωσε το ασκησιολόγιο της ' || to_char(new.target_date, 'DD/MM'),
           new.id
    from coaches c where c.id = auth_coach_id();
  end if;
  return new;
end $$;

drop trigger if exists trg_notify_workout_updated on workout_programs;
create trigger trg_notify_workout_updated after update of content, target_date, start_time on workout_programs
for each row execute function notify_workout_updated();
