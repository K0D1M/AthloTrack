-- AthloTrack migration 011: administrators and the in-app admin dashboard («Διαχείριση»).
-- Run in Supabase > SQL Editor. Safe to re-run.
--
-- Admins are a third kind of login (not a coach or athlete). They read everything only through
-- the admin_* functions below, which check is_admin() first; they get no direct table access.
-- Actions that need Supabase's auth admin API (create a login, set a password, delete a login)
-- are in the Edge Function "admin" (supabase/functions/admin), which runs with the service role.
--
-- Make yourself admin (once, after creating your login in Authentication > Users):
--   insert into admins (auth_user_id, full_name, email)
--   select id, 'Ονοματεπώνυμο', email from auth.users where email = 'you@mail.com';

create table if not exists admins (
  id           uuid primary key default gen_random_uuid(),
  auth_user_id uuid not null unique references auth.users(id) on delete cascade,
  full_name    text not null,
  email        text,
  created_at   timestamptz not null default now()
);
alter table admins enable row level security;

-- An admin reads their own row (to start their session). No client writes.
drop policy if exists own_admin_row on admins;
create policy own_admin_row on admins for select using (auth_user_id = auth.uid());

create or replace function is_admin() returns boolean
language sql stable security definer set search_path = public as $$
  select exists (select 1 from admins where auth_user_id = auth.uid())
$$;

create or replace function admin_guard() returns void
language plpgsql stable security definer set search_path = public as $$
begin
  if not is_admin() then
    raise exception 'not admin' using errcode = '42501';
  end if;
end $$;

-- What admins did, newest first in the dashboard. Written by the functions below and by the
-- Edge Function (service role); admins can read it, nobody can write it from the app.
create table if not exists admin_audit (
  id            uuid primary key default gen_random_uuid(),
  admin_auth_id uuid references auth.users(id) on delete set null,
  action        text not null,
  target        text,
  details       jsonb,
  created_at    timestamptz not null default now()
);
create index if not exists idx_admin_audit_created on admin_audit (created_at desc);
alter table admin_audit enable row level security;
drop policy if exists admins_read_audit on admin_audit;
create policy admins_read_audit on admin_audit for select using (is_admin());

create or replace function admin_log(p_action text, p_target text, p_details jsonb) returns void
language sql security definer set search_path = public as $$
  insert into admin_audit (admin_auth_id, action, target, details)
  values (auth.uid(), p_action, p_target, p_details)
$$;
-- Only the admin_* functions call it.
revoke execute on function admin_log(text, text, jsonb) from public, anon, authenticated;

-- ---------------------------------------------------------------- read ---

create or replace function admin_overview() returns jsonb
language plpgsql stable security definer set search_path = public as $$
begin
  perform admin_guard();
  return jsonb_build_object(
    'coaches',            (select count(*) from coaches),
    'athletes',           (select count(*) from athletes),
    'linked_athletes',    (select count(*) from athletes where auth_user_id is not null),
    'logins',             (select count(*) from auth.users),
    'active_7d',          (select count(*) from auth.users where last_sign_in_at >= now() - interval '7 days'),
    'active_30d',         (select count(*) from auth.users where last_sign_in_at >= now() - interval '30 days'),
    'app_users',          (select count(distinct auth_user_id) from device_tokens),
    'workouts_total',     (select count(*) from workout_programs),
    'workouts_week',      (select count(*) from workout_programs where created_at >= date_trunc('week', now())),
    'workouts_month',     (select count(*) from workout_programs where created_at >= date_trunc('month', now())),
    'due_30d',            (select count(*) from workout_programs
                            where target_date between current_date - 30 and current_date),
    'completed_30d',      (select count(*) from workout_programs
                            where target_date between current_date - 30 and current_date and completed_at is not null),
    'read_30d',           (select count(*) from workout_programs
                            where target_date between current_date - 30 and current_date and read_at is not null),
    'measurements_month', (select count(*) from measurements where created_at >= date_trunc('month', now()))
  );
end $$;

-- Newest events across everyone: notifications (new, edited and completed workouts), new
-- measurements, new athletes and new logins.
create or replace function admin_activity(p_limit int default 50) returns jsonb
language plpgsql stable security definer set search_path = public as $$
begin
  perform admin_guard();
  return coalesce((
    select jsonb_agg(to_jsonb(e) order by e.at desc)
    from (
      select * from (
        select 'notification:' || n.type as kind, n.created_at as at, a.full_name as who, n.message as text
        from notifications n join athletes a on a.id = n.athlete_id
        union all
        select 'measurement', m.created_at, a.full_name, 'Νέα μέτρηση: ' || m.weight_kg || ' kg'
        from measurements m join athletes a on a.id = m.athlete_id
        union all
        select 'athlete', a.created_at, c.full_name, 'Νέος αθλητής: ' || a.full_name
        from athletes a join coaches c on c.id = a.coach_id
        union all
        select 'signup', u.created_at, coalesce(u.email, ''), 'Νέος λογαριασμός'
        from auth.users u
      ) all_events
      order by at desc
      limit greatest(1, least(p_limit, 200))
    ) e
  ), '[]'::jsonb);
end $$;

-- Things to look at: one list per check.
create or replace function admin_health() returns jsonb
language plpgsql stable security definer set search_path = public as $$
begin
  perform admin_guard();
  return jsonb_build_object(
    -- Logins with no coach, athlete or admin profile (an athlete who signed up before being added).
    'orphan_logins', coalesce((
      select jsonb_agg(jsonb_build_object('id', u.id, 'title', coalesce(u.email, '—'),
               'detail', 'Λογαριασμός από ' || to_char(u.created_at, 'DD/MM/YYYY')) order by u.created_at desc)
      from auth.users u
      where not exists (select 1 from coaches c where c.auth_user_id = u.id)
        and not exists (select 1 from athletes a where a.auth_user_id = u.id)
        and not exists (select 1 from admins d where d.auth_user_id = u.id)), '[]'::jsonb),
    'athletes_no_email', coalesce((
      select jsonb_agg(jsonb_build_object('id', a.id, 'title', a.full_name, 'detail', 'Προπονητής: ' || c.full_name)
               order by a.full_name)
      from athletes a join coaches c on c.id = a.coach_id
      where coalesce(trim(a.email), '') = ''), '[]'::jsonb),
    'athletes_unlinked', coalesce((
      select jsonb_agg(jsonb_build_object('id', a.id, 'title', a.full_name, 'detail', a.email || ' · ' || c.full_name)
               order by a.full_name)
      from athletes a join coaches c on c.id = a.coach_id
      where coalesce(trim(a.email), '') <> '' and a.auth_user_id is null), '[]'::jsonb),
    'athletes_inactive', coalesce((
      select jsonb_agg(jsonb_build_object('id', x.id, 'title', x.full_name,
               'detail', 'Τελευταία δραστηριότητα: ' || to_char(x.last_at, 'DD/MM/YYYY') || ' · ' || x.coach)
               order by x.last_at)
      from (
        select a.id, a.full_name, c.full_name as coach,
               greatest(a.created_at,
                        coalesce((select max(w.created_at) from workout_programs w where w.athlete_id = a.id), a.created_at),
                        coalesce((select max(m.created_at) from measurements m where m.athlete_id = a.id), a.created_at)) as last_at
        from athletes a join coaches c on c.id = a.coach_id
      ) x
      where x.last_at < now() - interval '30 days'), '[]'::jsonb),
    'coaches_pending_password', coalesce((
      select jsonb_agg(jsonb_build_object('id', c.id, 'title', c.full_name,
               'detail', coalesce(c.email, '') || ' · από ' || to_char(c.created_at, 'DD/MM/YYYY')) order by c.created_at)
      from coaches c
      where c.must_set_password and c.created_at < now() - interval '7 days'), '[]'::jsonb),
    'orphan_devices', coalesce((
      select jsonb_agg(jsonb_build_object('id', t.auth_user_id, 'title', coalesce(u.email, '—'),
               'detail', t.platform || ' · ' || to_char(t.updated_at, 'DD/MM/YYYY')) order by t.updated_at desc)
      from device_tokens t left join auth.users u on u.id = t.auth_user_id
      where not exists (select 1 from coaches c where c.auth_user_id = t.auth_user_id)
        and not exists (select 1 from athletes a where a.auth_user_id = t.auth_user_id)
        and not exists (select 1 from admins d where d.auth_user_id = t.auth_user_id)), '[]'::jsonb)
  );
end $$;

create or replace function admin_coaches() returns jsonb
language plpgsql stable security definer set search_path = public as $$
begin
  perform admin_guard();
  return coalesce((
    select jsonb_agg(jsonb_build_object(
             'id', c.id, 'auth_user_id', c.auth_user_id, 'full_name', c.full_name, 'email', c.email,
             'created_at', c.created_at, 'must_set_password', c.must_set_password,
             'athletes', (select count(*) from athletes a where a.coach_id = c.id),
             'last_sign_in_at', u.last_sign_in_at,
             'has_app', exists (select 1 from device_tokens t where t.auth_user_id = c.auth_user_id))
           order by c.full_name)
    from coaches c left join auth.users u on u.id = c.auth_user_id
  ), '[]'::jsonb);
end $$;

create or replace function admin_athletes() returns jsonb
language plpgsql stable security definer set search_path = public as $$
begin
  perform admin_guard();
  return coalesce((
    select jsonb_agg(jsonb_build_object(
             'id', a.id, 'auth_user_id', a.auth_user_id, 'full_name', a.full_name, 'email', a.email,
             'coach_id', a.coach_id, 'coach_name', c.full_name, 'created_at', a.created_at,
             'last_sign_in_at', u.last_sign_in_at, 'last_activity', a.updated_at,
             'workouts', (select count(*) from workout_programs w where w.athlete_id = a.id),
             'measurements', (select count(*) from measurements m where m.athlete_id = a.id),
             'has_app', a.auth_user_id is not null
                        and exists (select 1 from device_tokens t where t.auth_user_id = a.auth_user_id))
           order by a.full_name)
    from athletes a join coaches c on c.id = a.coach_id
    left join auth.users u on u.id = a.auth_user_id
  ), '[]'::jsonb);
end $$;

-- Devices with the app (Android, and browsers that allowed notifications). The token itself stays
-- on the server.
create or replace function admin_devices() returns jsonb
language plpgsql stable security definer set search_path = public as $$
begin
  perform admin_guard();
  return coalesce((
    select jsonb_agg(jsonb_build_object(
             'auth_user_id', t.auth_user_id,
             'name', coalesce(c.full_name, a.full_name, d.full_name, u.email, '—'),
             'email', u.email,
             'role', case when c.id is not null then 'coach' when a.id is not null then 'athlete'
                          when d.id is not null then 'admin' else 'none' end,
             'platform', t.platform, 'updated_at', t.updated_at)
           order by t.updated_at desc)
    from device_tokens t
    left join auth.users u on u.id = t.auth_user_id
    left join coaches c on c.auth_user_id = t.auth_user_id
    left join athletes a on a.auth_user_id = t.auth_user_id
    left join admins d on d.auth_user_id = t.auth_user_id
  ), '[]'::jsonb);
end $$;

create or replace function admin_audit_log(p_limit int default 100) returns jsonb
language plpgsql stable security definer set search_path = public as $$
begin
  perform admin_guard();
  return coalesce((
    select jsonb_agg(to_jsonb(x) order by x.created_at desc)
    from (
      select l.created_at, l.action, l.target, l.details, coalesce(u.email, '—') as admin_email
      from admin_audit l left join auth.users u on u.id = l.admin_auth_id
      order by l.created_at desc
      limit greatest(1, least(p_limit, 500))
    ) x
  ), '[]'::jsonb);
end $$;

-- --------------------------------------------------------------- write ---

create or replace function admin_force_password_change(p_coach uuid) returns void
language plpgsql security definer set search_path = public as $$
declare v_name text;
begin
  perform admin_guard();
  update coaches set must_set_password = true where id = p_coach returning full_name into v_name;
  if v_name is null then raise exception 'coach not found'; end if;
  perform admin_log('force_password_change', v_name, jsonb_build_object('coach_id', p_coach));
end $$;

create or replace function admin_move_athlete(p_athlete uuid, p_coach uuid) returns void
language plpgsql security definer set search_path = public as $$
declare v_athlete text; v_from text; v_to text;
begin
  perform admin_guard();
  select full_name into v_to from coaches where id = p_coach;
  if v_to is null then raise exception 'coach not found'; end if;
  select a.full_name, c.full_name into v_athlete, v_from
  from athletes a join coaches c on c.id = a.coach_id where a.id = p_athlete;
  if v_athlete is null then raise exception 'athlete not found'; end if;
  update athletes set coach_id = p_coach, updated_at = now() where id = p_athlete;
  perform admin_log('move_athlete', v_athlete,
    jsonb_build_object('athlete_id', p_athlete, 'from', v_from, 'to', v_to));
end $$;

-- Deletes the athlete with their measurements, workouts and notifications (cascade). Their login
-- stays; delete it from the dashboard's coaches/athletes actions or Authentication > Users.
create or replace function admin_delete_athlete(p_athlete uuid) returns void
language plpgsql security definer set search_path = public as $$
declare v_name text; v_coach text;
begin
  perform admin_guard();
  select a.full_name, c.full_name into v_name, v_coach
  from athletes a join coaches c on c.id = a.coach_id where a.id = p_athlete;
  if v_name is null then raise exception 'athlete not found'; end if;
  delete from athletes where id = p_athlete;
  perform admin_log('delete_athlete', v_name, jsonb_build_object('athlete_id', p_athlete, 'coach', v_coach));
end $$;
