-- AthloTrack migration 010: repair created_at on rows the app inserted before 1.6.
-- The app used to send its empty default (0001-01-01) instead of letting the database set it.
-- Only rows with that impossible date are touched. Run once in Supabase > SQL Editor. Safe to re-run.

-- Workouts: the time of their "new workout" notification, which the database stamped correctly.
update workout_programs w
set created_at = n.created_at
from notifications n
where w.created_at < '1900-01-01'
  and n.related_workout_id = w.id and n.type = 'new_workout';

-- Any left (e.g. the notification was deleted): their target date, at noon.
update workout_programs
set created_at = target_date + time '12:00'
where created_at < '1900-01-01';

-- Measurements: the day they were measured, at noon.
update measurements
set created_at = measured_at + time '12:00'
where created_at < '1900-01-01';

-- Athletes added before 1.6 also got an empty updated_at, until their first measurement or workout.
update athletes
set updated_at = now()
where updated_at < '1900-01-01';

-- Athletes: their last update.
update athletes
set created_at = updated_at
where created_at < '1900-01-01' and updated_at > '1900-01-01';
update athletes
set created_at = now()
where created_at < '1900-01-01';
