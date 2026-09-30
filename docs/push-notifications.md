# Push notifications

## What happens

1. **A coach creates a workout.** Trigger `notify_new_workout` inserts a notification row with `recipient = 'athlete'`.
2. **The athlete taps «Ολοκληρώθηκε».** `completed_at` is set, and trigger `notify_workout_completed` inserts a row with `recipient = 'coach'`.
3. Every insert into `notifications` fires a **Database Webhook** that calls the Edge Function **`push`** (`supabase/functions/push/index.ts`).
4. The function looks up the recipient's login (the athlete, or their coach through `athletes.coach_id`), reads that user's rows in `device_tokens`, and sends each one a message through **Firebase Cloud Messaging (HTTP v1)**.
5. Android shows the notification, even when the app is closed. When the app is open, `AthloTrackMessagingService.OnMessageReceived` shows it itself.
6. **Tapping it opens Προπονήσεις.** The push carries `data.type`, and Android hands it to `MainActivity` as an intent extra. `MainActivity` is `SingleTop`, so a running app gets `OnNewIntent` instead of being recreated. `NotificationNavigation.RequestFor(type)` stores the request, and `MainView` applies it when it is showing. After a cold start, that happens once the session has been restored.

The notification row stays in the database either way, so the in-app **Ειδοποιήσεις** list on **Πρόσφατα** is still complete if a push is missed, and on the web.

### Why Firebase?
On Android, only Google's push service (FCM) can wake a closed app. Supabase has no push service of its own. Firebase is used **only** to deliver the message. All the data and logic stay in Supabase, and the FCM free tier covers this use.

## Device tokens

- After every sign-in or restored session, `App.ShowMain` calls `PushRegistrationService.RegisterAsync()`. It gets the Firebase token from `FirebaseTokenProvider` and calls the RPC `claim_device_token`, which assigns the token to the current user. When someone else signs in on the same phone, the token moves to them.
- On logout, `UnregisterAsync()` deletes the token row, and that phone stops receiving pushes for the account.
- When Firebase rotates the token, `OnNewToken` registers the new one.
- When FCM answers `UNREGISTERED` or `INVALID_ARGUMENT` (for example, the app was uninstalled), the function deletes that token.

## One-time setup (already done for the live project)

1. **Firebase:** go to [console.firebase.google.com](https://console.firebase.google.com), create a project and add an Android app with package **`com.k0d1m.athlotrack`**. Download `google-services.json` into `AthloTrack.Android/`. The file is git-ignored; keep a backup. Without it the app still builds, but it gets no push token.
2. **Service account:** in Firebase, open **Project settings → Service accounts → Generate new private key**. In Supabase, open **Edge Functions → Secrets** and add `FIREBASE_SERVICE_ACCOUNT` with the whole JSON file as its value. Don't commit this file.
3. **Database:** run `supabase/005_workout_completion_push.sql`.
4. **Function:** in Supabase, open **Edge Functions → Deploy a new function** and name it `push`, with the contents of `supabase/functions/push/index.ts`. Supabase provides `SUPABASE_URL` and `SUPABASE_SERVICE_ROLE_KEY` automatically.
5. **Webhook:** in Supabase, open **Database → Webhooks → Create**. Use table `notifications`, event **Insert**, type **Supabase Edge Function**, and choose `push`.

## Troubleshooting

| Symptom | Check |
|---|---|
| No push at all | Does `device_tokens` have a row for the recipient? (`select auth_user_id, platform, updated_at from device_tokens;`) If not, the app didn't register a token: `google-services.json` was missing at build time, or the user hasn't signed in since installing. |
| Token exists, still nothing | Look at **Edge Functions → push → Logs**, and at **Database → Webhooks** to confirm the webhook exists and fires on Insert. |
| Function error about credentials | `FIREBASE_SERVICE_ACCOUNT` is missing or isn't the full JSON. |
| Notification arrives but is silent or hidden | Android settings → Apps → AthloTrack → Notifications: are they allowed, and is the "AthloTrack" channel on? |
| Works on the phone, not in the browser | This is expected. The web app shows the in-app list only. |

## Limits
- Android only. Web browsers and iPhone get the in-app list only.
- A phone receives pushes only for the account currently signed in on it.
