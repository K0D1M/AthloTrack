-- AthloTrack migration 017: workouts from before the app («Παλιό ασκησιολόγιο»).
-- Run once in Supabase > SQL Editor (after 016). Safe to re-run.
--
-- A coach records an old workout as already answered (Ολοκληρώθηκε / Δεν ολοκληρώθηκε) through
-- add_past_workout(): it is marked from_archive, counts as read and accepted, and the athlete gets
-- no notification (so no push), no «Θα το κάνω» question and no reminder. Only that function sets
-- these fields at insert, through the athlotrack.archiving setting (like reminding / cancelling).

alter table workout_programs add column if not exists from_archive boolean not null default false;

-- 013's insert guard, plus: from_archive and the answer fields only through add_past_workout().
create or replace function guard_workout_insert() returns trigger
language plpgsql as $$
begin
  if coalesce(current_setting('athlotrack.archiving', true), '') = '1' then
    return new;
  end if;
  new.from_archive     := false;
  new.accepted_at      := null;
  new.declined_at      := null;
  new.decline_reason   := null;
  new.cancelled_at     := null;
  new.completed_at     := null;
  new.not_completed_at := null;
  new.last_reminded_at := null;
  return new;
end $$;

-- The athlete's «νέο ασκησιολόγιο» notification, not for an archived workout.
create or replace function notify_new_workout() returns trigger
language plpgsql security definer set search_path = public as $$
begin
  if new.from_archive then
    return new;
  end if;
  insert into notifications (athlete_id, type, message, related_workout_id)
  select new.athlete_id, 'new_workout',
         'Ο ' || c.full_name || ' πρόσθεσε καινούργιο ασκησιολόγιο', new.id
  from coaches c where c.id = new.created_by;
  return new;
end $$;

-- 014's guard, plus: nobody changes from_archive; editing an archived workout keeps it read.
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

-- 014's edit notification, not for an archived workout.
create or replace function notify_workout_updated() returns trigger
language plpgsql security definer set search_path = public as $$
begin
  if auth_coach_id() is not null and not new.from_archive
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

-- An old workout, already answered. Only the athlete's own coach; only a date before today.
create or replace function add_past_workout(
  p_athlete uuid, p_content text, p_date date, p_time time default null,
  p_completed boolean default true, p_coach_present boolean default null) returns uuid
language plpgsql security definer set search_path = public as $$
declare
  v_coach uuid := auth_coach_id();
  v_id uuid;
  v_answered timestamptz := (p_date + time '12:00') at time zone 'Europe/Athens';
begin
  if v_coach is null or not exists (select 1 from athletes where id = p_athlete and coach_id = v_coach) then
    raise exception 'not allowed' using errcode = '42501';
  end if;
  if p_date is null or p_date >= current_date then
    raise exception 'not past';
  end if;
  if char_length(btrim(coalesce(p_content, ''))) = 0 then
    raise exception 'empty';
  end if;

  perform set_config('athlotrack.archiving', '1', true);
  insert into workout_programs (athlete_id, content, target_date, start_time, coach_present, created_by,
                                from_archive, accepted_at, read_at, completed_at, not_completed_at)
  values (p_athlete, btrim(p_content), p_date, p_time, p_coach_present, v_coach,
          true, now(), now(),
          case when p_completed then v_answered end,
          case when p_completed then null else v_answered end)
  returning id into v_id;
  perform set_config('athlotrack.archiving', '', true);
  return v_id;
end $$;

revoke execute on function add_past_workout(uuid, text, date, time, boolean, boolean) from public, anon;
grant execute on function add_past_workout(uuid, text, date, time, boolean, boolean) to authenticated;
