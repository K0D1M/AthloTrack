# Push notifications

## What happens

1. **A coach creates a workout** (`notify_new_workout`, type `new_workout`) **or changes one** (`notify_workout_updated`, type `workout_updated`). A notification row with `recipient = 'athlete'` is inserted.
2. **The athlete taps «Ολοκληρώθηκε».** `completed_at` is set, and trigger `notify_workout_completed` inserts a row with `recipient = 'coach'` (type `workout_completed`).
3. Every insert into `notifications` fires a **Database Webhook** that calls the Edge Function **`push`** (`supabase/functions/push/index.ts`).
4. The function looks up the recipient's login (the athlete, or their coach through `athletes.coach_id`), reads that user's rows in `device_tokens`, and sends each one a message through **Firebase Cloud Messaging (HTTP v1)**.
5. Android shows the notification, even when the app is closed. When the app is open, `AthloTrackMessagingService.OnMessageReceived` shows it itself.
6. **Tapping it opens Προπονήσεις** and marks the matching in-app notification as read. The push carries `data.type` and `data.notificationId`, and Android hands them to `MainActivity` as intent extras. `MainActivity` is `SingleTop`, so a running app gets `OnNewIntent` instead of being recreated. `NotificationNavigation.RequestFor(type, notificationId)` stores the request, and `MainView` applies it when it is showing. After a cold start, that happens once the session has been restored.

The notification row stays in the database either way, so the in-app **Ειδοποιήσεις** list on **Πρόσφατα** is still complete if a push is missed, and on the web.

### Why Firebase?
On Android, only Google's push service (FCM) can wake a closed app. Supabase has no push service of its own. Firebase is used **only** to deliver the message. All the data and logic stay in Supabase, and the FCM free tier covers this use.

## Web and iPhone

The web app gets the same pushes through **Firebase web push**, using the same Firebase project and the same `push` function. On iPhone, this works only from the web app added to the Home Screen (iOS 16.4+); there is no native iOS app.
- `AthloTrack.Browser/wwwroot/firebase-config.js` holds the web app's Firebase config and the Web Push (VAPID) key. Both are public. While that file is empty, the web app shows no notification options.
- `push.js` wraps the Firebase SDK, and `firebase-messaging-sw.js` is the service worker that shows notifications while the app is closed. `Services/BrowserPush.cs` connects them to `IPushTokenProvider` (platform `web`) and `IPushPermission`.
- Browsers only show the permission prompt after a tap, so web push is turned on with **«Ενεργοποίηση ειδοποιήσεων»**. It sits in a banner on **Πρόσφατα** until notifications are on, and always in **Ρυθμίσεις**. On an iPhone that isn't running from the Home Screen, the banner explains how to add the app first.
- The function adds `webpush.fcm_options.link = <app>/?type=…&notification=…` (base URL from the optional `APP_URL` secret). Tapping the notification opens that link, and the app opens Προπονήσεις and marks the notification read.

## Device tokens

- After every sign-in or restored session, `App.ShowMain` calls `PushRegistrationService.RegisterAsync()`. It gets the Firebase token from `FirebaseTokenProvider` and calls the RPC `claim_device_token`, which assigns the token to the current user. When someone else signs in on the same phone, the token moves to them.
- On logout, `UnregisterAsync()` deletes the token row, and that phone stops receiving pushes for the account.
- When Firebase rotates the token, `OnNewToken` registers the new one.
- When FCM answers `UNREGISTERED` or `INVALID_ARGUMENT` (for example, the app was uninstalled), the function deletes that token.

## One-time setup (already done for the live project)

1. **Firebase:** go to [console.firebase.google.com](https://console.firebase.google.com), create a project and add an Android app with package **`com.k0d1m.athlotrack`**. Download `google-services.json` into `AthloTrack.Android/` (it's in git). For the web, also add a **Web app**, copy its config, and under **Cloud Messaging → Web Push certificates** generate a key pair. Both go into `wwwroot/firebase-config.js`.
2. **Service account:** in Firebase, open **Project settings → Service accounts → Generate new private key**. In Supabase, open **Edge Functions → Secrets** and add `FIREBASE_SERVICE_ACCOUNT` with the whole JSON file as its value. Don't commit this file.
3. **Database:** run `supabase/005_workout_completion_push.sql`.
4. **Function:** in Supabase, open **Edge Functions → Deploy a new function** and name it `push`, with the contents of `supabase/functions/push/index.ts`. Supabase provides `SUPABASE_URL` and `SUPABASE_SERVICE_ROLE_KEY` automatically.
5. **Webhook:** in Supabase, open **Database → Webhooks → Create**. Use table `notifications`, event **Insert**, type **Supabase Edge Function**, and choose `push`.

## Admin test pushes and announcements
The admin dashboard («Ειδοποιήσεις») sends pushes through the `admin` Edge Function, which calls `push` with a **direct** payload: `{ "direct": { "auth_user_ids": [...], "title": "...", "body": "..." } }`.
- `push` accepts a direct payload only with the service-role key as the bearer token, so only the `admin` function can send one.
- It goes to every device of those logins, with type `admin`.
- No `notifications` row is written, so it doesn't appear in Πρόσφατα.

Re-deploy `push` from `supabase/functions/push/index.ts` after updating it. The `admin` function is deployed the same way (see [administration.md](administration.md#admin-dashboard)).

## Troubleshooting

| Symptom | Check |
|---|---|
| No push at all | Does `device_tokens` have a row for the recipient? (`select auth_user_id, platform, updated_at from device_tokens;`) If not, the app didn't register a token: `google-services.json` was missing at build time, or the user hasn't signed in since installing. |
| Token exists, still nothing | Look at **Edge Functions → push → Logs**, and at **Database → Webhooks** to confirm the webhook exists and fires on Insert. |
| Function error about credentials | `FIREBASE_SERVICE_ACCOUNT` is missing or isn't the full JSON. |
| Notification arrives but is silent or hidden | Android settings → Apps → AthloTrack → Notifications: are they allowed, and is the "AthloTrack" channel on? |
| Nothing in the browser | Ρυθμίσεις → Ειδοποιήσεις: is it «ενεργές»? If it says blocked, allow notifications for the site in the browser's settings. Is there a `platform = 'web'` token for the user? |
| Nothing on iPhone | Is the app opened from the Home Screen icon (not Safari), with iOS 16.4+, and notifications enabled in Ρυθμίσεις? |

## Limits
- iPhone works only through the Home Screen web app. There is no native iOS app, which would need a Mac and an Apple developer account.
- A device receives pushes only for the account currently signed in on it.
- The Android app 1.1 still gets Firebase-posted notifications, which Android bundles when 4 or more pile up; tapping that bundle only opens the app. From 1.2 the app posts them itself (see below).

## Android: the app's own notification group (1.2+)

App 1.2 registers its token as platform **`android-data`**, and the function sends those tokens **data-only** messages (title and body in `data`). So `AthloTrackMessagingService.OnMessageReceived` runs even with the app in the background, and `PushNotifications.Show` posts each notification in the group `athlotrack_workouts`. From 2 notifications on, it adds a summary («N νέες ειδοποιήσεις»). Tapping the summary opens Προπονήσεις, marks all of the group's notifications read (their ids are passed comma-separated) and clears them. Tokens registered as `android` (app 1.1) keep getting notification messages, so older installs keep working until they're updated.
