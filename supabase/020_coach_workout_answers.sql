-- AthloTrack migration 020: the coach marks a workout «Ολοκληρώθηκε» / «Δεν ολοκληρώθηκε».
-- Run once in Supabase > SQL Editor (after 019). Safe to re-run.
--
-- Until now only the athlete answered. The coach can now answer for them, through
-- coach_answer_workout(), on any workout that isn't cancelled or already answered: also one the
-- athlete never accepted (e.g. an athlete without the app) or declined and did after all.
-- answered_by records who answered ('athlete' / 'coach'). The athlete is notified of the coach's
-- mark (the same types the coach gets, addressed to the athlete, so older apps show it too). The
-- coach can undo their own mark through coach_undo_answer(); the athlete's answer stays final.
--
-- Only the guard's body changes here; its trigger (with 019's dashboard skip) stays as it is.

begin;

alter table workout_programs add column if not exists answered_by text;
alter table workout_programs drop constraint if exists workout_programs_answered_by_check;
alter table workout_programs add constraint workout_programs_answered_by_check
  check (answered_by is null or answered_by in ('athlete', 'coach'));

-- Answers so far were the athletes' (archived workouts, recorded by the coach, stay unmarked).
-- athlotrack.marking lets the guard keep the value if it runs here.
select set_config('athlotrack.marking', '1', true);
update workout_programs set answered_by = 'athlete'
where answered_by is null and not from_archive and (completed_at is not null or not_completed_at is not null);
select set_config('athlotrack.marking', '', true);

-- 017's guard, plus: answered_by follows the answer; the coach's answer fields change only
-- through coach_answer_workout() / coach_undo_answer() (the athlotrack.marking setting).
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
    new.from_archive     := old.from_archive;

    -- The choice: once, final, not on a cancelled or answered workout (the coach may have answered
    -- first). «Θα το κάνω» wins if both arrive.
    if old.accepted_at is not null or old.declined_at is not null or old.cancelled_at is not null
       or old.completed_at is not null or old.not_completed_at is not null then
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
      new.answered_by      := old.answered_by;
    elsif new.completed_at is not null then
      new.not_completed_at := null;
      new.answered_by      := 'athlete';
    elsif new.not_completed_at is not null then
      new.answered_by      := 'athlete';
    else
      new.answered_by      := old.answered_by;
    end if;
  else
    -- The coach (or anyone else): the choice is the athlete's; the answer only through the functions.
    new.accepted_at      := old.accepted_at;
    new.declined_at      := old.declined_at;
    new.decline_reason   := old.decline_reason;
    if coalesce(current_setting('athlotrack.marking', true), '') <> '1' then
      new.completed_at     := old.completed_at;
      new.not_completed_at := old.not_completed_at;
      new.answered_by      := old.answered_by;
    end if;
    if coalesce(current_setting('athlotrack.reminding', true), '') <> '1' then
      new.last_reminded_at := old.last_reminded_at;
    end if;
    if coalesce(current_setting('athlotrack.cancelling', true), '') <> '1' then
      new.cancelled_at := old.cancelled_at;
    end if;
    new.from_archive := old.from_archive;

    if old.from_archive then
      new.read_at := old.read_at;  -- archived: was never "sent", so an edit doesn't make it unread
    elsif new.content is distinct from old.content
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

-- The answer -> the other side is notified: the athlete's answer to the coach (as before), the
-- coach's mark to the athlete.
create or replace function notify_workout_completed() returns trigger
language plpgsql security definer set search_path = public as $$
begin
  if old.completed_at is null and new.completed_at is not null then
    if new.answered_by = 'coach' then
      insert into notifications (athlete_id, recipient, type, message, related_workout_id)
      select new.athlete_id, 'athlete', 'workout_completed',
             'Ο ' || c.full_name || ' σημείωσε ότι ολοκλήρωσες το ασκησιολόγιο της ' || to_char(new.target_date, 'DD/MM'),
             new.id
      from athletes a join coaches c on c.id = a.coach_id where a.id = new.athlete_id;
    else
      insert into notifications (athlete_id, recipient, type, message, related_workout_id)
      select new.athlete_id, 'coach', 'workout_completed',
             'Ο/Η ' || a.full_name || ' ολοκλήρωσε το ασκησιολόγιο της ' || to_char(new.target_date, 'DD/MM'),
             new.id
      from athletes a where a.id = new.athlete_id;
    end if;
  end if;
  return new;
end $$;

create or replace function notify_workout_not_completed() returns trigger
language plpgsql security definer set search_path = public as $$
begin
  if old.not_completed_at is null and new.not_completed_at is not null then
    if new.answered_by = 'coach' then
      insert into notifications (athlete_id, recipient, type, message, related_workout_id)
      select new.athlete_id, 'athlete', 'workout_not_completed',
             'Ο ' || c.full_name || ' σημείωσε ότι δεν ολοκλήρωσες το ασκησιολόγιο της ' || to_char(new.target_date, 'DD/MM'),
             new.id
      from athletes a join coaches c on c.id = a.coach_id where a.id = new.athlete_id;
    else
      insert into notifications (athlete_id, recipient, type, message, related_workout_id)
      select new.athlete_id, 'coach', 'workout_not_completed',
             'Ο/Η ' || a.full_name || ' δεν ολοκλήρωσε το ασκησιολόγιο της ' || to_char(new.target_date, 'DD/MM'),
             new.id
      from athletes a where a.id = new.athlete_id;
    end if;
  end if;
  return new;
end $$;

-- The coach answers for the athlete. Only the athlete's own coach; not on a cancelled workout or
-- one already answered.
create or replace function coach_answer_workout(p_workout uuid, p_completed boolean) returns void
language plpgsql security definer set search_path = public as $$
declare
  v_coach uuid := auth_coach_id();
  w workout_programs;
begin
  select * into w from workout_programs where id = p_workout for update;
  if v_coach is null or w.id is null
     or not exists (select 1 from athletes where id = w.athlete_id and coach_id = v_coach) then
    raise exception 'not allowed' using errcode = '42501';
  end if;
  if w.cancelled_at is not null then
    raise exception 'cancelled';
  end if;
  if w.completed_at is not null or w.not_completed_at is not null then
    raise exception 'already answered';
  end if;

  perform set_config('athlotrack.marking', '1', true);
  update workout_programs
     set completed_at     = case when p_completed then now() end,
         not_completed_at = case when p_completed then null else now() end,
         answered_by      = 'coach'
   where id = p_workout;
  perform set_config('athlotrack.marking', '', true);
end $$;

-- The coach takes back their own mark (never the athlete's answer); the athlete's unread notice goes too.
create or replace function coach_undo_answer(p_workout uuid) returns void
language plpgsql security definer set search_path = public as $$
declare
  v_coach uuid := auth_coach_id();
  w workout_programs;
begin
  select * into w from workout_programs where id = p_workout for update;
  if v_coach is null or w.id is null
     or not exists (select 1 from athletes where id = w.athlete_id and coach_id = v_coach) then
    raise exception 'not allowed' using errcode = '42501';
  end if;
  if w.answered_by is distinct from 'coach' then
    raise exception 'not the coach''s answer';
  end if;

  perform set_config('athlotrack.marking', '1', true);
  update workout_programs
     set completed_at = null, not_completed_at = null, answered_by = null
   where id = p_workout;
  perform set_config('athlotrack.marking', '', true);

  delete from notifications
   where related_workout_id = p_workout and recipient = 'athlete' and not is_read
     and type in ('workout_completed', 'workout_not_completed');
end $$;

revoke execute on function coach_answer_workout(uuid, boolean) from public, anon;
grant execute on function coach_answer_workout(uuid, boolean) to authenticated;
revoke execute on function coach_undo_answer(uuid) from public, anon;
grant execute on function coach_undo_answer(uuid) to authenticated;

commit;
