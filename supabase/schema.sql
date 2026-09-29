-- FitTrack Supabase schema: run this once in the Supabase SQL editor
-- (Project > SQL Editor > New query) after creating the project.

create table coaches (
  id uuid primary key default gen_random_uuid(),
  auth_user_id uuid unique references auth.users(id) on delete cascade,
  full_name text not null,
  email text,
  created_at timestamptz not null default now()
);

create table athletes (
  id uuid primary key default gen_random_uuid(),
  auth_user_id uuid unique references auth.users(id) on delete cascade,
  coach_id uuid not null references coaches(id) on delete cascade,
  full_name text not null,
  date_of_birth date,
  height_cm numeric(5,1),
  profile_image_path text,
  notes text,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);
create index idx_athletes_coach on athletes (coach_id);

create table measurements (
  id uuid primary key default gen_random_uuid(),
  athlete_id uuid not null references athletes(id) on delete cascade,
  measured_at date not null default current_date,
  weight_kg numeric(5,2) not null,
  fat_mass_wt numeric(5,2),
  fat_hgt numeric(5,2),
  created_by uuid references coaches(id),
  created_at timestamptz not null default now()
);
create index idx_measurements_athlete_date on measurements (athlete_id, measured_at);

create table workout_programs (
  id uuid primary key default gen_random_uuid(),
  athlete_id uuid not null references athletes(id) on delete cascade,
  title text,
  content text not null,
  target_date date not null,
  created_by uuid references coaches(id),
  created_at timestamptz not null default now()
);
create index idx_workouts_athlete_date on workout_programs (athlete_id, target_date);

create table notifications (
  id uuid primary key default gen_random_uuid(),
  athlete_id uuid not null references athletes(id) on delete cascade,
  type text not null default 'new_workout',
  message text not null,
  related_workout_id uuid references workout_programs(id) on delete set null,
  is_read boolean not null default false,
  created_at timestamptz not null default now()
);
create index idx_notifications_athlete on notifications (athlete_id, is_read, created_at desc);

-- Keep athletes.updated_at current whenever a measurement or workout is added — drives "Πρόσφατα".
create or replace function touch_athlete_updated_at() returns trigger as $$
begin
  update athletes set updated_at = now() where id = new.athlete_id;
  return new;
end; $$ language plpgsql security definer;

create trigger trg_touch_athlete_on_measurement after insert on measurements
for each row execute function touch_athlete_updated_at();

create trigger trg_touch_athlete_on_workout after insert on workout_programs
for each row execute function touch_athlete_updated_at();

-- Auto-create the athlete's in-app notification when a coach adds a new workout program.
create or replace function notify_new_workout() returns trigger as $$
begin
  insert into notifications (athlete_id, type, message, related_workout_id)
  select new.athlete_id, 'new_workout',
         'Ο ' || c.full_name || ' πρόσθεσε καινούργιο ασκησιολόγιο', new.id
  from coaches c where c.id = new.created_by;
  return new;
end; $$ language plpgsql security definer;

create trigger trg_notify_new_workout after insert on workout_programs
for each row execute function notify_new_workout();

-- Row Level Security: a coach has full access to their own athletes' data;
-- an athlete can only read their own data (and mark their own notifications as read).
alter table coaches enable row level security;
alter table athletes enable row level security;
alter table measurements enable row level security;
alter table workout_programs enable row level security;
alter table notifications enable row level security;

create policy coach_manage_own_row on coaches
  for all using (auth_user_id = auth.uid());

create policy coach_manage_own_athletes on athletes
  for all using (coach_id in (select id from coaches where auth_user_id = auth.uid()));

create policy athlete_read_own_row on athletes
  for select using (auth_user_id = auth.uid());

create policy coach_manage_measurements on measurements
  for all using (athlete_id in (
    select a.id from athletes a join coaches c on a.coach_id = c.id where c.auth_user_id = auth.uid()
  ));

create policy athlete_read_own_measurements on measurements
  for select using (athlete_id in (select id from athletes where auth_user_id = auth.uid()));

create policy coach_manage_workouts on workout_programs
  for all using (athlete_id in (
    select a.id from athletes a join coaches c on a.coach_id = c.id where c.auth_user_id = auth.uid()
  ));

create policy athlete_read_own_workouts on workout_programs
  for select using (athlete_id in (select id from athletes where auth_user_id = auth.uid()));

create policy coach_manage_notifications on notifications
  for all using (athlete_id in (
    select a.id from athletes a join coaches c on a.coach_id = c.id where c.auth_user_id = auth.uid()
  ));

create policy athlete_read_own_notifications on notifications
  for select using (athlete_id in (select id from athletes where auth_user_id = auth.uid()));

create policy athlete_update_own_notifications on notifications
  for update using (athlete_id in (select id from athletes where auth_user_id = auth.uid()));

-- Storage: private bucket for athlete profile photos, one row created manually via
-- Storage UI ("avatars", private) or: insert into storage.buckets (id, name, public) values ('avatars','avatars', false);
create policy coach_manage_avatar_files on storage.objects
  for all using (
    bucket_id = 'avatars'
    and (storage.foldername(name))[1]::uuid in (
      select a.id from athletes a join coaches c on a.coach_id = c.id where c.auth_user_id = auth.uid()
    )
  );

create policy athlete_read_own_avatar on storage.objects
  for select using (
    bucket_id = 'avatars'
    and (storage.foldername(name))[1]::uuid in (select id from athletes where auth_user_id = auth.uid())
  );
