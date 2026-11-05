-- 018: «Έντυπα». The coach writes a form (formatted intro text + questions) and sends it to
-- chosen athletes; each athlete fills in their own copy and submits it once. Only that coach
-- sees the answers; the athlete keeps a read-only copy. Safe to run again.

-- ---------------------------------------------------------------------------
-- 1. Tables
-- ---------------------------------------------------------------------------
create table if not exists forms (
  id         uuid primary key default gen_random_uuid(),
  coach_id   uuid not null references coaches(id) on delete cascade,
  -- The coach's name for the form's PDF: the athlete can't read coaches.
  coach_name text not null default '',
  title      text not null check (length(btrim(title)) > 0),
  intro      text not null default '',
  -- [{ "id", "kind": short|long|yesno|choice|date, "label", "required", "options": [...] }]
  questions  jsonb not null default '[]'::jsonb check (jsonb_typeof(questions) = 'array'),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);
create index if not exists idx_forms_coach on forms (coach_id, created_at desc);

create table if not exists form_assignments (
  id           uuid primary key default gen_random_uuid(),
  form_id      uuid not null references forms(id) on delete cascade,
  athlete_id   uuid not null references athletes(id) on delete cascade,
  -- { "<question id>": "answer" }; written only by submit_form, so the coach never sees a draft.
  answers      jsonb,
  submitted_at timestamptz,
  created_at   timestamptz not null default now(),
  unique (form_id, athlete_id)
);
create index if not exists idx_form_assignments_athlete on form_assignments (athlete_id, created_at desc);

alter table notifications add column if not exists related_form_assignment_id uuid
  references form_assignments(id) on delete set null;

grant select, insert, update, delete on forms, form_assignments to authenticated;

-- ---------------------------------------------------------------------------
-- 2. Who sees what
-- ---------------------------------------------------------------------------
alter table forms enable row level security;
alter table form_assignments enable row level security;

-- Lookups across the two tables without RLS (each table's policy reading the other would recurse).
create or replace function auth_coach_form_ids() returns setof uuid
language sql stable security definer set search_path = public as $$
  select id from forms where coach_id = auth_coach_id()
$$;
create or replace function auth_athlete_form_ids() returns setof uuid
language sql stable security definer set search_path = public as $$
  select form_id from form_assignments where athlete_id = auth_athlete_id()
$$;

drop policy if exists coach_manage_forms on forms;
create policy coach_manage_forms on forms
  for all using (coach_id = auth_coach_id()) with check (coach_id = auth_coach_id());

-- An athlete reads only the forms sent to them.
drop policy if exists athlete_read_assigned_forms on forms;
create policy athlete_read_assigned_forms on forms
  for select using (id in (select auth_athlete_form_ids()));

-- The coach sends their own forms to their own athletes, reads the copies and deletes them.
-- No update: answers go in only through submit_form.
drop policy if exists coach_read_assignments on form_assignments;
create policy coach_read_assignments on form_assignments
  for select using (form_id in (select auth_coach_form_ids()));
drop policy if exists coach_insert_assignments on form_assignments;
create policy coach_insert_assignments on form_assignments
  for insert with check (
    form_id in (select auth_coach_form_ids())
    and athlete_id in (select id from athletes where coach_id = auth_coach_id())
    and answers is null and submitted_at is null);
drop policy if exists coach_delete_assignments on form_assignments;
create policy coach_delete_assignments on form_assignments
  for delete using (form_id in (select auth_coach_form_ids()));

drop policy if exists athlete_read_own_assignments on form_assignments;
create policy athlete_read_own_assignments on form_assignments
  for select using (athlete_id = auth_athlete_id());

-- ---------------------------------------------------------------------------
-- 3. Guards: the coach's name, and questions locked once a copy is submitted
-- ---------------------------------------------------------------------------
create or replace function guard_form() returns trigger
language plpgsql security definer set search_path = public as $$
begin
  new.coach_name := coalesce((select full_name from coaches where id = new.coach_id), '');
  if tg_op = 'UPDATE' then
    new.coach_id := old.coach_id;
    new.created_at := old.created_at;
    if new.questions is distinct from old.questions
       and exists (select 1 from form_assignments where form_id = old.id and submitted_at is not null) then
      raise exception 'locked' using errcode = 'P0001';
    end if;
  end if;
  new.updated_at := now();
  return new;
end $$;

drop trigger if exists trg_guard_form on forms;
create trigger trg_guard_form before insert or update on forms
  for each row execute function guard_form();

-- ---------------------------------------------------------------------------
-- 4. Notifications: «νέο έντυπο» to the athlete, «υπέβαλε» to the coach
-- ---------------------------------------------------------------------------
create or replace function notify_new_form() returns trigger
language plpgsql security definer set search_path = public as $$
begin
  insert into notifications (athlete_id, recipient, type, message, related_form_assignment_id)
  select new.athlete_id, 'athlete', 'new_form',
         'Ο ' || f.coach_name || ' σου έστειλε το έντυπο «' || f.title || '» να το συμπληρώσεις', new.id
  from forms f where f.id = new.form_id;
  return new;
end $$;

drop trigger if exists trg_notify_new_form on form_assignments;
create trigger trg_notify_new_form after insert on form_assignments
  for each row execute function notify_new_form();

-- ---------------------------------------------------------------------------
-- 5. submit_form: the athlete's answers, once
-- ---------------------------------------------------------------------------
create or replace function submit_form(p_assignment uuid, p_answers jsonb) returns timestamptz
language plpgsql security definer set search_path = public as $$
declare
  a form_assignments%rowtype;
  f forms%rowtype;
  q jsonb;
  v text;
  v_at timestamptz := now();
begin
  select * into a from form_assignments where id = p_assignment;
  if not found or a.athlete_id is distinct from auth_athlete_id() then
    raise exception 'not allowed' using errcode = '42501';
  end if;
  if a.submitted_at is not null then
    raise exception 'already submitted' using errcode = 'P0001';
  end if;
  if p_answers is null or jsonb_typeof(p_answers) <> 'object' then
    raise exception 'missing' using errcode = 'P0001';
  end if;

  select * into f from forms where id = a.form_id;
  for q in select * from jsonb_array_elements(f.questions) loop
    if coalesce((q ->> 'required')::boolean, false) then
      v := btrim(coalesce(p_answers ->> (q ->> 'id'), ''));
      if v = '' then
        raise exception 'missing' using errcode = 'P0001';
      end if;
    end if;
  end loop;

  update form_assignments set answers = p_answers, submitted_at = v_at where id = a.id;
  update athletes set updated_at = v_at where id = a.athlete_id;

  insert into notifications (athlete_id, recipient, type, message, related_form_assignment_id)
  select a.athlete_id, 'coach', 'form_submitted',
         'Ο/Η ' || ath.full_name || ' υπέβαλε το έντυπο «' || f.title || '»', a.id
  from athletes ath where ath.id = a.athlete_id;
  return v_at;
end $$;

revoke all on function submit_form(uuid, jsonb) from public, anon;
grant execute on function submit_form(uuid, jsonb) to authenticated;
