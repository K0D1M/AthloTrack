# Architecture

```
            ┌──────────────── AthloTrack (shared Avalonia UI) ───────────────┐
            │ Views (.axaml)  ·  ViewModels (CommunityToolkit.Mvvm)  ·  Styles │
            └───────────────────────────────┬─────────────────────────────────┘
                                            │
                    AthloTrack.Core: models · repositories · auth · push
                                            │  supabase-csharp (anon key + user JWT)
                                            ▼
     Supabase: Postgres + RLS · Auth · Storage "avatars" · Edge Function "push"
                                            │  FCM HTTP v1
                                            ▼
                               Firebase Cloud Messaging → Android phones

  Platform heads: AthloTrack.Browser (WASM) · AthloTrack.Android · AthloTrack.Desktop · AthloTrack.WinUI (paused)
```

The app talks to Supabase directly: it has no backend server of its own. Access control lives in the database (Row Level Security). The client only holds the public anon key and the signed-in user's session.

## Projects

### AthloTrack.Core
This project has no UI dependencies.
- `Models/` holds the domain types (`Athlete`, `Coach`, `Measurement`, `WorkoutProgram`, `AppNotification`).
- `Supabase/Rows/` holds the Postgrest row models. These are mapped to the domain types in the repositories.
- `Data/` holds the repositories (`IAthleteRepository`, `IMeasurementRepository`, `IWorkoutRepository`, `INotificationRepository`, `ICoachRepository`) and `AvatarService` (the private `avatars` bucket, read through signed URLs).
- `Auth/` holds `AuthService` (sign in and sign up, and role detection from the `coaches` and `athletes` tables), `SessionInitializer` (restores the saved session at start-up), `SessionState` (the current user and role) and `ICredentialStore`. Each platform provides its own credential store.
- `Push/PushRegistrationService.cs` registers this device's push token for the signed-in user through the RPC `claim_device_token`, and removes it on logout. `IPushTokenProvider` is supplied by the Android head. On other heads no token provider is registered, so push registration does nothing.
- `DependencyInjection/CoreServiceRegistration.cs` registers all of the above.

A Postgrest predicate can't contain method calls. Compute values such as `Guid.Parse(...)` into a local variable first, then use the variable in `.Where(x => x.Id == id)`.

### AthloTrack (shared UI)
- `App.axaml.cs` builds the DI container, restores the session and chooses the root view: `LoginView`, `SetPasswordView` or `MainView`. The root is a single `ContentControl` whose content is swapped, because Android reads the main view only once, when the activity is created.
- `ViewModels/` holds one view model per screen, plus factories for the ones that need parameters (for example `AthleteProfileViewModelFactory`). They don't reference Avalonia types; photos are passed around as `byte[]`. That is also why the WinUI head can reuse them unchanged.
- `Views/` holds the views, resolved by `ViewLocator` by name. `Views/Controls/` has `Avatar` and `GreekDatePicker`, a date picker with Greek ημέρα/μήνας/έτος placeholders.
- `Services/PhotoPicker.cs` and `Services/PhotoProcessor.cs` handle photo selection and resize the photo before upload.
- `Styles/Theme.axaml` holds the brand colours and gradients, and `Styles/Icons.axaml` the path icons.
- `Assets/supabase.config.json` holds the Supabase URL and anon key. It is embedded and read by `AppBootstrap.LoadSupabaseConfig`.
- `AppBootstrap.RegisterPlatformServices` is how each head registers its own services (credential store, push token provider).

The progress chart (LiveCharts2) on the athlete profile is created in code-behind (`AthleteProfileView.BuildChart`) only after the data has loaded. When the chart was declared in XAML it sometimes stayed blank on Android.

### Platform heads
| Head | Credential store | Notes |
|---|---|---|
| `AthloTrack.Browser` | `localStorage` | WASM build: needs `WasmBuildNative` for SkiaSharp, `TrimmerRootAssembly` for the Supabase and Newtonsoft assemblies (reflection), and the el-GR culture pinned in `wwwroot/main.js` |
| `AthloTrack.Android` | `session.json` in the app's private files folder | Firebase messaging service, notification channel `athlotrack`, and the `POST_NOTIFICATIONS` permission on Android 13+ |
| `AthloTrack.Desktop` | DPAPI-encrypted file in `%LOCALAPPDATA%\AthloTrack` | The quickest way to run the app during development |
| `AthloTrack.WinUI` | Windows | Paused. Links the shared view models through `Compile Include` |

## Roles in the UI
`SessionState.Role` is `Coach` or `Athlete`, and the shared screens adapt to it:
- A coach sees all of their athletes, the **+** menus (new athlete, measurement, workout) and the completion notifications.
- An athlete sees only their own profile. `IsOwnProfile` shows the **Ολοκληρώθηκε** button and the photo editing. They also see their coach and their new-workout notifications.

The database enforces these rules no matter what the UI shows. See [database.md](database.md).

## Package versions
Versions are set centrally in `Directory.Packages.props`. Keep all Avalonia packages on the same version. LiveCharts uses `2.1.0-dev`, its Avalonia 12 line; the stable 2.0.x releases fail at runtime with Avalonia 12.
