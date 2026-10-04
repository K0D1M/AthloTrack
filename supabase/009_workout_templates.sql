-- AthloTrack migration 009: the coach's saved workout templates («Πρότυπα» in the workout editor).
-- Run once in Supabase > SQL Editor (after 008). Safe to re-run.

create table if not exists workout_templates (
  id uuid primary key default gen_random_uuid(),
  coach_id uuid not null default auth_coach_id() references coaches(id) on delete cascade,
  name text not null,
  content text not null,
  created_at timestamptz not null default now()
);
create index if not exists idx_workout_templates_coach on workout_templates (coach_id, created_at desc);

-- Each coach sees and manages only their own templates; athletes see none.
alter table workout_templates enable row level security;
drop policy if exists coach_own_templates on workout_templates;
create policy coach_own_templates on workout_templates
  for all using (coach_id = auth_coach_id()) with check (coach_id = auth_coach_id());
