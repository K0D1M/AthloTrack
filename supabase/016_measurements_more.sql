-- AthloTrack migration 016: weight optional, measurement notes, renaming/deleting saved fields.
-- Run once in Supabase > SQL Editor (after 015). Safe to re-run.
--
-- A measurement may now be only «Λιπομέτρηση», only «Άλλες μετρήσεις» or only notes: weight_kg is
-- optional. notes is free text with the Ασκησιολόγιο markup (## , - , 1. , **…**). A coach renames
-- a field by updating it (015's policy); deleting one also removes its values from every
-- measurement, through delete_measurement_field().

alter table measurements alter column weight_kg drop not null;

alter table measurements add column if not exists notes text;
alter table measurements drop constraint if exists measurements_notes_length;
alter table measurements add constraint measurements_notes_length
  check (notes is null or char_length(notes) <= 4000);

-- How many measurements have a value for one of the caller's fields.
create or replace function measurement_field_usage(p_field uuid) returns int
language plpgsql stable security definer set search_path = public as $$
declare
  v_coach uuid := auth_coach_id();
begin
  if v_coach is null or not exists (select 1 from measurement_fields where id = p_field and coach_id = v_coach) then
    raise exception 'not allowed' using errcode = '42501';
  end if;
  return (select count(*) from measurements m join athletes a on a.id = m.athlete_id
          where a.coach_id = v_coach and m.extra ? p_field::text);
end $$;

-- Deletes one of the caller's fields and its values from every measurement (an emptied extra
-- becomes null). Returns how many values were removed.
create or replace function delete_measurement_field(p_field uuid) returns int
language plpgsql security definer set search_path = public as $$
declare
  v_coach uuid := auth_coach_id();
  v_count int;
begin
  if v_coach is null or not exists (select 1 from measurement_fields where id = p_field and coach_id = v_coach) then
    raise exception 'not allowed' using errcode = '42501';
  end if;

  with changed as (
    update measurements m
       set extra = nullif(m.extra - p_field::text, '{}'::jsonb)
      from athletes a
     where a.id = m.athlete_id and a.coach_id = v_coach and m.extra ? p_field::text
    returning m.id
  )
  select count(*) into v_count from changed;

  delete from measurement_fields where id = p_field and coach_id = v_coach;
  return v_count;
end $$;

revoke execute on function measurement_field_usage(uuid) from public, anon;
revoke execute on function delete_measurement_field(uuid) from public, anon;
grant execute on function measurement_field_usage(uuid) to authenticated;
grant execute on function delete_measurement_field(uuid) to authenticated;
