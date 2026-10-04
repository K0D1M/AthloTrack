# Database (Supabase)

The schema lives in `supabase/`. On a fresh project, run the files **in order** in **SQL Editor**. The migrations (002–010) are safe to run again.

| File | Adds |
|---|---|
| `schema.sql` | Base tables, the `notify_new_workout` trigger, base RLS policies, avatar storage policies |
| `002_athlete_accounts.sql` | `athletes.email`, linking a login to an athlete by email, athlete self-edit, coach photo, RLS helper functions, the `avatars` bucket |
| `003_athlete_owns_avatar.sql` | Coaches can view and remove athlete photos but not upload them. Only the athlete sets their own photo |
| `004_coach_first_password.sql` | `coaches.must_set_password` (first-sign-in password page) |
| `005_workout_completion_push.sql` | `workout_programs.completed_at`, coach notifications (`notifications.recipient`), `device_tokens`, RPC `claim_device_token` |
| `006_workout_read_receipts.sql` | `workout_programs.read_at` (read receipt), RPC `mark_workouts_read` (used by app 1.0 only) |
| `007_workout_edits_and_seen.sql` | A coach edit clears the receipt and notifies the athlete (`workout_updated`); RPC `mark_workout_read(p_workout)` for per-workout receipts |
| `008_workout_coach_presence.sql` | `workout_programs.coach_present` (the coach will be there: true Παρών, false Απών, null not stated); changing it counts as an edit (receipt cleared, athlete notified) |
| `009_workout_templates.sql` | Table `workout_templates`: the coach's saved workout texts («Πρότυπα» in the editor) |
| `010_fix_created_at.sql` | Data repair: before 1.6 the app stored `created_at` (and a new athlete's `updated_at`) as 0001-01-01; this sets real values. The row models now leave `created_at` to the database (`ignoreOnInsert`) |

## Tables

| Table | Main columns | Notes |
|---|---|---|
| `coaches` | `auth_user_id`, `full_name`, `email`, `profile_image_path`, `must_set_password` | One row per coach login. Created by an administrator |
| `athletes` | `coach_id`, `auth_user_id` (nullable until linked), `full_name`, `email`, `date_of_birth`, `height_cm`, `profile_image_path`, `notes`, `updated_at` | `updated_at` is bumped by every new measurement or workout. The **Πρόσφατα** screen sorts by it |
| `measurements` | `athlete_id`, `measured_at`, `weight_kg`, `fat_mass_wt`, `fat_hgt` | |
| `workout_programs` | `athlete_id`, `title`, `content`, `target_date`, `coach_present`, `completed_at`, `read_at` | `completed_at` is null while the workout is open. `read_at` is when the athlete first saw it. `coach_present` is optional (null shows no indicator). `content` is plain text with a small markup (`## `, `- `, `1. `, `**…**`) that the app shows formatted |
| `workout_templates` | `coach_id`, `name`, `content`, `created_at` | `coach_id` defaults to `auth_coach_id()` |
| `notifications` | `athlete_id`, `recipient` (`athlete` / `coach`), `type`, `message`, `related_workout_id`, `is_read` | Every insert triggers a phone push, see [push-notifications.md](push-notifications.md) |
| `device_tokens` | `token` (PK), `auth_user_id`, `platform` | The Firebase token of each installed Android app |

Deleting an athlete also deletes their measurements, workouts and notifications (cascade).

## Security (Row Level Security)

RLS is enabled on every table, so what each user can read or change is enforced by the database. The policies use three `security definer` helper functions, which avoid recursive policy checks:

- `auth_coach_id()`: the `coaches.id` of the signed-in user, if they are a coach
- `auth_athlete_id()`: the `athletes.id` of the signed-in user, if they are an athlete
- `auth_athlete_coach_id()`: that athlete's coach

| Who | Can |
|---|---|
| Coach | Read and write their own `coaches` row, their own athletes, those athletes' measurements, workouts and notifications, and their own `workout_templates` |
| Athlete | Read their own athlete row, measurements and workouts, their coach's row, and notifications addressed to them (`recipient = 'athlete'`). Update their own profile and mark their workouts as done |

Two triggers stop athletes from editing more than they should:
- `guard_athlete_self_edit`: when an athlete updates their own row, `coach_id`, `auth_user_id`, `email`, `notes` and `created_at` keep their old values. They can change only their name, date of birth, height and photo.
- `guard_workout_athlete_edit`: when an athlete updates a workout, everything except `completed_at` and a first `read_at` keeps its old value. A coach can't set `read_at`; when a coach changes `content`, `target_date` or `coach_present`, `read_at` is cleared, because the athlete hasn't seen the new version yet.

The RPC `mark_workout_read(p_workout)` (`security definer`) sets `read_at = now()` on one of the caller's own workouts, the first time only. The app calls it once a workout has been on the athlete's screen (at least half of it) for a second (`Views/Controls/SeenTracker.cs`), and the coach then sees «Διαβάστηκε από τον αθλητή στις …».

## Triggers that generate data

| Trigger | When | Does |
|---|---|---|
| `trg_notify_new_workout` | A workout is inserted | Inserts an athlete notification: «… πρόσθεσε καινούργιο ασκησιολόγιο» |
| `trg_notify_workout_completed` | `completed_at` changes from null to a value | Inserts a coach notification: «Ο/Η {name} ολοκλήρωσε το ασκησιολόγιο της DD/MM» |
| `trg_notify_workout_updated` | A coach changes `content` or `target_date` | Inserts an athlete notification: «Ο {coach} ενημέρωσε το ασκησιολόγιο της DD/MM» |
| `trg_touch_athlete_on_measurement` / `_on_workout` | A measurement or workout is inserted | Bumps `athletes.updated_at` |
| `trg_link_athlete_on_signup` (on `auth.users`) | A login is created | Links it to the athlete with the same email |
| `trg_link_athlete_on_email` | An athlete's email is set or changed | Links the athlete to an existing login with that email |

## Storage

The private bucket `avatars` holds photos at `avatars/{athlete_id}/…` and `avatars/{coach_id}/…`. The app reads them through signed URLs.

| Who | Own folder | Other folders |
|---|---|---|
| Athlete | Read and write | Read their coach's photo |
| Coach | Read and write | Read and delete their athletes' photos (no upload) |

## Changing the schema

Add a new numbered file (`008_….sql`) instead of editing old ones. Make it safe to re-run (`if not exists`, `drop policy if exists`, `create or replace`), run it in **SQL Editor** and commit it. `.gitignore` ignores `*.sql` except `supabase/*.sql`.
