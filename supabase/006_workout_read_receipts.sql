-- AthloTrack migration 006: read receipts — the coach sees when the athlete has seen a workout.
-- Run once in Supabase > SQL Editor (after 005). Safe to re-run.

alter table workout_programs add column if not exists read_at timestamptz;

-- Same guard as 005, plus read_at: the athlete sets it once (never moves it); nobody else can.
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
  else
    new.read_at     := old.read_at;
  end if;
  return new;
end $$;

-- The athlete opened a screen listing their workouts: stamp every unread one.
create or replace function mark_workouts_read() returns void
language sql security definer set search_path = public as $$
  update workout_programs set read_at = now()
  where athlete_id = auth_athlete_id() and read_at is null;
$$;
grant execute on function mark_workouts_read() to authenticated;
