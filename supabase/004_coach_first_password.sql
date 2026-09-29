-- AthloTrack migration 004: coaches set their own password on first sign-in.
-- Run once in Supabase > SQL Editor. Safe to re-run.

-- Every coach (existing and new) must choose their own password at their next sign-in.
alter table coaches add column if not exists must_set_password boolean not null default true;
update coaches set must_set_password = true;

-- Creating a coach from now on (after adding the login in Authentication > Users):
--   insert into coaches (auth_user_id, full_name, email)
--   select id, 'Ονοματεπώνυμο', email from auth.users where email = 'coach@mail.com';
-- must_set_password defaults to true, so the page appears on their first sign-in.
-- To require it again for someone later:  update coaches set must_set_password = true where email = '...';
