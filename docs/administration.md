# Administration

All of these tasks are done in the Supabase dashboard of the project. No email provider is configured, so Supabase can't send confirmation or password-recovery emails. The steps below avoid email completely.

## Auth settings

- **Authentication → Sign In / Providers → Email → Confirm email: off.** New athlete accounts can then sign in straight away, without a confirmation email.
- Don't use the dashboard's "Send password recovery" button. Its link points to the Site URL (by default `http://localhost:3000`), and the app has no recovery page. To reset a password, follow [Resetting a password](#resetting-a-password) below.

## Creating a coach

Coaches don't sign themselves up. An administrator creates their account:

1. **Authentication → Users → Add user → Create new user.** Enter the coach's email and a temporary password, and tick *Auto Confirm User*.
2. **SQL Editor**, then run:
   ```sql
   insert into coaches (auth_user_id, full_name, email)
   select id, 'Ονοματεπώνυμο', email from auth.users where email = 'coach@mail.com';
   ```
3. Send the coach the email and the temporary password. Because `must_set_password` defaults to `true`, the app asks them to choose their own password the first time they sign in.

To make a coach choose a new password again at their next sign-in, run:
```sql
update coaches set must_set_password = true where email = 'coach@mail.com';
```

## Linking athletes to their accounts

Linking works by email and happens automatically, whichever step comes first:

- **The coach adds the athlete first** (with an email), and **the athlete signs up later** with the same email. On signup, trigger `link_athlete_on_signup` links the account to the profile.
- **The athlete signs up first**, and **the coach adds the athlete or sets their email later**. Trigger `link_athlete_on_email` finds the existing login and links it.

Emails are compared case-insensitively. Each login links to at most one athlete, and each athlete email must be unique.

A login without a matching athlete profile can sign in but has nothing to see until the coach adds them.

To check which athletes have a linked account:
```sql
select full_name, email, auth_user_id is not null as linked from athletes order by full_name;
```

## Resetting a password

Run this in the **SQL Editor**, with the new password and the user's email:
```sql
update auth.users
set encrypted_password = crypt('NewPassword123', gen_salt('bf'))
where email = 'user@mail.com';
```
For a coach, also run the `must_set_password = true` update above, so they replace the temporary password with their own.

## Removing people

- **Delete an athlete** (from the app, or with `delete from athletes where …`). Their measurements, workouts and notifications are deleted with them. Their login stays in **Authentication → Users**, where you can delete it too.
- **Delete a login** in **Authentication → Users**. Its coach or athlete row is deleted along with it (`on delete cascade`). Deleting a coach's login removes all of that coach's athletes.

## Testing

The live project has **no test accounts** (they were removed on 2026-10-02). For a test, create throwaway accounts, never use real ones, and delete them afterwards:
1. Create a test coach as in [Creating a coach](#creating-a-coach) (e.g. `coach.test@example.com`), and a test athlete under it with a throwaway email.
2. When you're done:
   ```sql
   delete from auth.users where email in ('coach.test@example.com', '<test athlete email>');
   ```
   This also removes the test coach's profile, athletes, workouts, notifications and push tokens (cascade).

The integration tests in `AthloTrack.Tests` use such accounts through environment variables; see [architecture.md](architecture.md#tests).
