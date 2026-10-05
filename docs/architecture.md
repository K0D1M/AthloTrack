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
- `Auth/` holds `AuthService` (sign in and sign up, and role detection from the `coaches` and `athletes` tables), `AuthErrors` (Supabase Auth errors in Greek), `SessionInitializer` (restores the saved session at start-up), `SessionState` (the current user and role), `ICredentialStore` and `IAppPreferences`. Each platform provides its own credential store and preferences store (`localStorage`, Android `SharedPreferences`, a file under `%LOCALAPPDATA%\AthloTrack\prefs`); without one, an in-memory store is used.
- `Push/PushRegistrationService.cs` registers this device's push token for the signed-in user through the RPC `claim_device_token`, and removes it on logout. `IPushTokenProvider` is supplied by the Android head. On other heads no token provider is registered, so push registration does nothing.
- `Workouts/WorkoutMarkup.cs` holds the workout-text markup (`## ` heading, `- ` bullet, `1. ` numbered, `**bold**`; any other line stays as typed) and the editor's helpers: bold and line-prefix toggles, list continuation on a new line, and the exercise builder's line format. It is pure and unit-tested. The content stays plain text in the database, so notifications, older app versions and old workouts are unaffected.
- `DependencyInjection/CoreServiceRegistration.cs` registers all of the above.

Three Postgrest-csharp pitfalls:
- Don't update a `date` column with `.Set(x => x.TargetDate, someDateTime)`: it is sent as UTC, so a local midnight in Greece lands on the previous day. Update the whole row model instead (see `WorkoutRepository.UpdateAsync`).
- A predicate can't contain method calls. Compute values such as `Guid.Parse(...)` into a local variable first, then use the variable in `.Where(x => x.Id == id)`.
- Don't combine **three or more** conditions with `&&` in one `Where`: the library builds a nested filter that PostgREST rejects (`PGRST100`). Chain one `.Where(...)` per condition instead (see `NotificationRepository.GetUnreadForAthleteAsync`).

### AthloTrack (shared UI)
- `App.axaml.cs` builds the DI container, restores the session and chooses the root view: `LoginView`, `SetPasswordView` or `MainView`. The root is a single `ContentControl` whose content is swapped, because Android reads the main view only once, when the activity is created.
- `ViewModels/` holds one view model per screen, plus factories for the ones that need parameters (for example `AthleteProfileViewModelFactory`). They don't reference Avalonia types; photos are passed around as `byte[]`. That is also why the WinUI head can reuse them unchanged.
- `Views/` holds the views, resolved by `ViewLocator` by name. `Views/Controls/` has:
  - `Avatar`, and `GreekDatePicker` (a date picker with Greek ημέρα/μήνας/έτος placeholders)
  - `SeenTracker`, an attached property that runs a command once its control has been on screen for a second (workout read receipts)
  - `PlotView`, which shows a ScottPlot chart as an image
  - `WorkoutText`, which shows a workout's text formatted, and `CoachPresence`, the coach's Παρών/Απών dot
- `Services/NotificationNavigation.cs` carries a tapped push's request (open Προπονήσεις, mark the notification read) to `MainView`, and `Services/ChartFonts.cs` gives the chart the bundled Inter font.
- `Services/PhotoPicker.cs` and `Services/PhotoProcessor.cs` handle photo selection and resize the photo before upload.
- `Styles/Theme.axaml` holds the brand colours and gradients, and `Styles/Icons.axaml` the path icons.
- `Assets/supabase.config.json` holds the Supabase URL and anon key. It is embedded and read by `AppBootstrap.LoadSupabaseConfig`.
- `AppBootstrap.RegisterPlatformServices` is how each head registers its own services (credential store, push token provider).

The workout editor (`AddWorkoutView`) handles the toolbar in its code-behind, because it needs the TextBox selection; the logic itself is in `WorkoutMarkup`. List continuation reacts to the text change rather than the Enter key, so phone keyboards behave the same. `AddWorkoutViewModel` keeps an unsaved draft in `IAppPreferences` (key `workout.draft.new.{athleteId}` or `workout.draft.edit.{workoutId}`), written on every change and cleared by Αποθήκευση, Άκυρο or Απόρριψη. An edit draft records a fingerprint of the workout it started from, and is dropped if the workout has changed since.

The progress chart works in two steps. `AthleteProfileViewModel` produces plain data (`ProgressChart`: date labels plus one `ChartLine` per metric), and `AthleteProfileView.BuildChart` draws it with **ScottPlot** (core package only) into `PlotView`. ScottPlot's own Avalonia control froze the browser's single UI thread, so the app renders the chart to an image at the control's pixel size instead.

### Platform heads
| Head | Credential store | Notes |
|---|---|---|
| `AthloTrack.Browser` | `localStorage` | WASM build: needs `WasmBuildNative` for SkiaSharp, `TrimmerRootAssembly` for the Supabase and Newtonsoft assemblies (reflection), and the el-GR culture pinned in `wwwroot/main.js` |
| `AthloTrack.Android` | `session.bin` in the app's private files folder, AES-256-GCM encrypted with a key kept in the Android Keystore (older plain `session.json` files are migrated) | Firebase messaging service, notification channel `athlotrack`, and the `POST_NOTIFICATIONS` permission on Android 13+ |
| `AthloTrack.Desktop` | DPAPI-encrypted file in `%LOCALAPPDATA%\AthloTrack` | The quickest way to run the app during development |
| `AthloTrack.WinUI` | Windows | Paused, and no longer builds: it links the shared view models through `Compile Include`, and they now use features it doesn't have (plain chart data, edit/seen commands) |

## Tests
`AthloTrack.Tests` (xUnit v3) has two kinds of tests:
- **Unit tests** for the view models and helpers, using in-memory fakes of the repositories (`Fakes.cs`).
- **Integration tests** that run the real queries against Supabase as the test accounts. They catch filters PostgREST rejects. They are skipped unless `ATHLOTRACK_TEST_ATHLETE_EMAIL`/`_PASSWORD` and `ATHLOTRACK_TEST_COACH_EMAIL`/`_PASSWORD` are set. Use throwaway test accounts only: the live project keeps none, so create them for the run and delete them afterwards (see [administration.md](administration.md#testing)).

```powershell
dotnet test --project AthloTrack.Tests
```

## Android keyboards
Avalonia's Android TextBox can lose keystrokes when the keyboard **composes** words, which Gboard and Samsung do with suggestions on. You get e.g. only «te» of «test», or nothing at all, especially on slower phones or a busy first launch. `App.axaml` therefore sets `TextInputOptions.ShowSuggestions = False` on every TextBox, so the keyboard types each key directly. Greek, including accents via long-press, still works. Don't add `TextInputOptions.ContentType="Email"` to a field: combined with that setting, it produces an input type in which Gboard composes again. Upstream, Avalonia is rewriting this code ([PR #20890](https://github.com/AvaloniaUI/Avalonia/pull/20890)); once a release fixes it, the style can go.

Multi-line boxes (`AcceptsReturn`, e.g. the workout text) also get `TextInputOptions.Multiline` and `ReturnKeyType=Return` from a second `App.axaml` style:
- **Why:** Avalonia didn't tell Android they were multi-line, so Gboard showed a ✓ "Done" key. Avalonia turned that action into a line break Gboard didn't know about, and Gboard then dropped the next key.
- **Result:** with the style, Gboard shows ⏎ and every key arrives.
- **Cost:** Avalonia drops the no-suggestions flag on multi-line boxes, so Gboard composes words there again. Tests with fast typing lost nothing; the losses seen before were on the login screen during a busy first launch.

The phone's keyboard doesn't resize the app's view. `AddWorkoutView` therefore listens to `TopLevel.InputPane` and, while the keyboard is open, ends the form above it and scrolls the line being typed into view.

## Motion and loading
Animations are subtle (about 200 ms), because the web build has one UI thread and phones can be slow.
- `Styles/Motion.axaml`:
  - `.appear` fades and lifts a control in when it's created; with `.appear-shown` it does so when a bound `shown` class switches on.
  - `ItemsControl.fadeitems` fades each list row in.
  - `Border.skeleton` is a pulsing placeholder, and `ProgressBar.topline` is the thin loading line along a screen's top.
  - Brand buttons scale slightly when pressed, and the `Spinner` control turns.
- `Views/Motion.cs` fades a page in when a section opens (`MainView.Show`) and fades the root in on login and logout (`App.SetRoot`). It animates the new content instead of using a `TransitioningContentControl`, because the sections are nested `ContentPage`s.
- Screens with `IsLoading` also have `ShowSkeleton`, which is true only on their **first** load: placeholders shaped like the content show then. Later reloads keep the content and show only the top line.
- Save buttons show a `Spinner` while `IsBusy`, and keep their brand colours, dimmed, while disabled.
- The login form focuses Email when it opens. At start-up it waits until `LoginViewModel.IsReady`, after the silent sign-in attempt, so a signed-in user never sees the keyboard flash.
- `Views/KeyboardInset.cs` reports how much of a view the phone keyboard covers, because the app's view isn't resized for it. The login screen and the workout editor use it to keep their content above the keyboard.

## Roles in the UI
The login screen has two steps. First the user picks **Προπονητής** or **Αθλητής** (or creates an athlete account). That choice decides which profile table `SessionInitializer` checks the login against. `LoginViewModel` remembers the role of the last successful login (`IAppPreferences`, key `login.last_role`) and opens that form directly next time.

`SessionState.Role` is `Coach` or `Athlete`, and the shared screens adapt to it:
- A coach sees all of their athletes, the **+** menus (new athlete, measurement, workout) and the completion notifications.
- An athlete sees only their own profile. `IsOwnProfile` shows the **Ολοκληρώθηκε** button and the photo editing. They also see their coach and their new-workout notifications.

The database enforces these rules no matter what the UI shows. See [database.md](database.md).

## Package versions
Versions are set centrally in `Directory.Packages.props`.
- Keep all Avalonia packages on the same version.
- `SkiaSharp.HarfBuzz` is pinned to Avalonia's SkiaSharp version. ScottPlot only asks for 3.119.0, which lacks a text-shaping method it calls, so the chart failed with "Method not found".
- `Xamarin.AndroidX.Lifecycle.Process` is pinned to match the Lifecycle version Avalonia brings.
- LiveCharts was replaced by ScottPlot: LiveCharts only had a `2.1.0-dev` preview for Avalonia 12.
