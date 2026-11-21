-- AthloTrack migration 021: an optional title for a measurement («Τεστ Cooper»).
-- Run once in Supabase > SQL Editor (after 020). Safe to re-run.
--
-- Shown in the athlete's profile and in Ημερολόγιο instead of plain «Μέτρηση». Null when empty.
-- The existing policies (coach_manage_measurements, athlete_read_own_measurements) cover it.

alter table measurements add column if not exists title text;
alter table measurements drop constraint if exists measurements_title_length;
alter table measurements add constraint measurements_title_length
  check (title is null or char_length(title) <= 80);

-- Ημερολόγιο lists a month's measurements, several a day: keep their order stable.
create index if not exists idx_measurements_date on measurements (measured_at, created_at);
