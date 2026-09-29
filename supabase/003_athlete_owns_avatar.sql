-- AthloTrack migration 003: only the athlete changes their own photo.
-- Coaches can still see athlete photos, and delete them when deleting the athlete.
-- Run once in Supabase > SQL Editor (after 002). Safe to re-run.

-- Storage: replace the coach's full access to athlete folders with read + delete.
drop policy if exists coach_manage_avatar_files on storage.objects;
drop policy if exists coach_read_athlete_avatars on storage.objects;
drop policy if exists coach_delete_athlete_avatars on storage.objects;

create policy coach_read_athlete_avatars on storage.objects
  for select using (
    bucket_id = 'avatars'
    and (storage.foldername(name))[1] in (select a.id::text from athletes a where a.coach_id = auth_coach_id())
  );

create policy coach_delete_athlete_avatars on storage.objects
  for delete using (
    bucket_id = 'avatars'
    and (storage.foldername(name))[1] in (select a.id::text from athletes a where a.coach_id = auth_coach_id())
  );

-- Table: a coach updating an athlete row can't change its photo.
create or replace function guard_athlete_self_edit() returns trigger
language plpgsql as $$
begin
  if old.auth_user_id = auth.uid() then
    -- the athlete editing themselves: photo, name, date of birth, height only
    new.coach_id     := old.coach_id;
    new.auth_user_id := old.auth_user_id;
    new.email        := old.email;
    new.notes        := old.notes;
    new.created_at   := old.created_at;
  else
    -- the coach: everything except the athlete's photo
    new.profile_image_path := old.profile_image_path;
  end if;
  return new;
end $$;
