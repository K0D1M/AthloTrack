# AthloTrack

AthloTrack is a Greek-language app that coaches and their athletes use to follow the athletes' progress. Coaches keep a profile for each athlete, record body measurements, and assign workout programs (ασκησιολόγια). Athletes sign in to see their own progress, mark workouts as done and keep their profile photo up to date. Their phones get a notification when a new program arrives, and the coach gets one when a program is completed.

*νοῦς ὑγιής ἐν σώματι ὑγιεῖ*

| | |
|---|---|
| **Web app** | https://athlotrack.up.railway.app |
| **Android** | Sideloaded APK (package `com.k0d1m.athlotrack`) |
| **Backend** | Supabase (Postgres, Row Level Security, Auth, Storage, Edge Functions) |
| **UI** | C# / .NET 10, [Avalonia 12](https://avaloniaui.net) (one shared UI for web, Android and desktop) |

## Features

- **Two roles.** Coaches manage their own athletes. Athletes see only their own data.
- **Athlete profiles:** photo, date of birth, height and notes.
- **Measurements:** weight, Fat Mass/WT and fat/hgt, with a progress chart once there are two or more.
- **Workout programs:** free-text programs with a target date. The athlete marks each one as **Ολοκληρώθηκε** (done).
- **Calendar** of workouts and measurements, plus a **Πρόσφατα** screen with the most recently active athletes and unread notifications.
- **Notifications.** A new workout notifies the athlete, and a completed workout notifies the coach. Each one is kept in the app and also sent as an Android push through Firebase Cloud Messaging.
- **Accounts.** An athlete's login links to their profile automatically when its email matches the one the coach entered. Coaches choose their own password the first time they sign in.

## Repository layout

| Path | What it is |
|---|---|
| `AthloTrack.Core/` | Models, Supabase repositories, auth/session and push registration. Has no UI dependencies. |
| `AthloTrack/` | Shared Avalonia UI: views, view models, styles and assets |
| `AthloTrack.Browser/` | Web head (WebAssembly), deployed to Railway |
| `AthloTrack.Android/` | Android head, including Firebase push |
| `AthloTrack.Desktop/` | Windows desktop head, used for local development |
| `AthloTrack.WinUI/` | WinUI 3 head. *Paused.* |
| `supabase/` | Database schema, migrations and the `push` Edge Function |
| `Dockerfile`, `Caddyfile` | Web build and static hosting on Railway |

## Quick start

Requirements: the .NET 10 SDK. For the web head, also run `dotnet workload install wasm-tools`. For the Android head, you need the Android SDK and `dotnet workload install android`.

```powershell
# Desktop (quickest way to try changes)
dotnet run --project AthloTrack.Desktop

# Web, served locally
dotnet run --project AthloTrack.Browser

# Android: build and install on a connected device or emulator
dotnet build AthloTrack.Android -c Debug -t:Install
```

The Supabase project URL and public (anon) key are read from `AthloTrack/Assets/supabase.config.json`. See [docs/deployment.md](docs/deployment.md) to point the app at a different Supabase project.

## Documentation

| Document | For |
|---|---|
| [docs/user-guide.md](docs/user-guide.md) | Coaches and athletes using the app (in Greek) |
| [docs/administration.md](docs/administration.md) | Creating coaches, linking athletes, resetting passwords |
| [docs/architecture.md](docs/architecture.md) | How the code is organised |
| [docs/database.md](docs/database.md) | Tables, security rules, triggers and migrations |
| [docs/push-notifications.md](docs/push-notifications.md) | How phone notifications work and how to set them up |
| [docs/deployment.md](docs/deployment.md) | Railway web hosting, Android builds, Supabase keep-alive |

## Brand

| Colour | Hex |
|---|---|
| Primary blue | `#1565C0` |
| Gold | `#FFD700` |
| White | `#FFFFFF` |
| Text | `#333333` |
