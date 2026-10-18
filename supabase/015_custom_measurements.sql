-- AthloTrack migration 015: «Άλλες μετρήσεις», the coach's own measurement fields.
-- Run once in Supabase > SQL Editor (after 014). Safe to re-run.
--
-- A coach creates a field once (name + optional unit, e.g. «Περιφέρεια μέσης», cm) and fills it in
-- any measurement. The values live in measurements.extra as {"<field id>": number}; the existing
-- measurements policies cover them. The athlete can read their coach's field names.

create table if not exists measurement_fields (
  id         uuid primary key default gen_random_uuid(),
  coach_id   uuid not null references coaches(id) on delete cascade,
  name       text not null check (char_length(btrim(name)) between 1 and 40),
  unit       text check (unit is null or char_length(unit) <= 12),
  created_at timestamptz not null default now()
);
create unique index if not exists uq_measurement_fields_coach_name
  on measurement_fields (coach_id, lower(btrim(name)));

alter table measurement_fields enable row level security;

drop policy if exists coach_manage_measurement_fields on measurement_fields;
create policy coach_manage_measurement_fields on measurement_fields
  for all using (coach_id = auth_coach_id()) with check (coach_id = auth_coach_id());

drop policy if exists athlete_read_coach_measurement_fields on measurement_fields;
create policy athlete_read_coach_measurement_fields on measurement_fields
  for select using (coach_id = (select a.coach_id from athletes a where a.id = auth_athlete_id()));

-- Names and units arrive trimmed; an empty unit is no unit.
create or replace function tidy_measurement_field() returns trigger
language plpgsql as $$
begin
  new.name := btrim(new.name);
  new.unit := nullif(btrim(coalesce(new.unit, '')), '');
  return new;
end $$;

drop trigger if exists trg_tidy_measurement_field on measurement_fields;
create trigger trg_tidy_measurement_field before insert or update on measurement_fields
for each row execute function tidy_measurement_field();

alter table measurements add column if not exists extra jsonb;
