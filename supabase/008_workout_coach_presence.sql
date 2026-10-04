-- AthloTrack migration 008: the coach says whether they'll be at a workout (Παρών / Απών).
-- null = not stated. Run once in Supabase > SQL Editor (after 007). Safe to re-run.

alter table workout_programs add column if not exists coach_present boolean;

-- Same guard as 007, plus: an athlete can't change coach_present, and a coach changing it
-- clears the read receipt (the athlete hasn't seen the change yet).
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
  elsif new.content is distinct from old.content
     or new.target_date is distinct from old.target_date
     or new.coach_present is distinct from old.coach_present then
    new.read_at       := null;
  else
    new.read_at       := old.read_at;
  end if;
  return new;
end $$;

-- The coach changed the program, its date or their presence: tell the athlete.
create or replace function notify_workout_updated() returns trigger
language plpgsql security definer set search_path = public as $$
begin
  if auth_coach_id() is not null
     and (new.content is distinct from old.content
          or new.target_date is distinct from old.target_date
          or new.coach_present is distinct from old.coach_present) then
    insert into notifications (athlete_id, recipient, type, message, related_workout_id)
    select new.athlete_id, 'athlete', 'workout_updated',
           'Ο ' || c.full_name || ' ενημέρωσε το ασκησιολόγιο της ' || to_char(new.target_date, 'DD/MM'),
           new.id
    from coaches c where c.id = auth_coach_id();
  end if;
  return new;
end $$;

drop trigger if exists trg_notify_workout_updated on workout_programs;
create trigger trg_notify_workout_updated after update of content, target_date, coach_present on workout_programs
for each row execute function notify_workout_updated();
