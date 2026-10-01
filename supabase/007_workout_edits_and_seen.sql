-- AthloTrack migration 007: coaches edit workouts (receipt resets, athlete notified);
-- athletes' read receipts are per workout. Run once in Supabase > SQL Editor (after 006). Safe to re-run.

-- Same guard as 006, except a coach edit of the program clears the read receipt:
-- the athlete hasn't seen the new version yet.
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
    new.read_at     := coalesce(old.read_at, new.read_at);
  elsif new.content is distinct from old.content or new.target_date is distinct from old.target_date then
    new.read_at     := null;
  else
    new.read_at     := old.read_at;
  end if;
  return new;
end $$;

-- The coach changed the program: tell the athlete (in-app + push via the notifications webhook).
create or replace function notify_workout_updated() returns trigger
language plpgsql security definer set search_path = public as $$
begin
  if auth_coach_id() is not null
     and (new.content is distinct from old.content or new.target_date is distinct from old.target_date) then
    insert into notifications (athlete_id, recipient, type, message, related_workout_id)
    select new.athlete_id, 'athlete', 'workout_updated',
           'Ο ' || c.full_name || ' ενημέρωσε το ασκησιολόγιο της ' || to_char(new.target_date, 'DD/MM'),
           new.id
    from coaches c where c.id = auth_coach_id();
  end if;
  return new;
end $$;

drop trigger if exists trg_notify_workout_updated on workout_programs;
create trigger trg_notify_workout_updated after update of content, target_date on workout_programs
for each row execute function notify_workout_updated();

-- The athlete has seen this one workout on screen: stamp it (once).
create or replace function mark_workout_read(p_workout uuid) returns void
language sql security definer set search_path = public as $$
  update workout_programs set read_at = now()
  where id = p_workout and athlete_id = auth_athlete_id() and read_at is null;
$$;
grant execute on function mark_workout_read(uuid) to authenticated;
