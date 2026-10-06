# Deployment

## Supabase configuration

Every head reads the project URL and public key from **`AthloTrack/Assets/supabase.config.json`**. The file is embedded in the app at build time:

```json
{
  "Url": "https://<project-ref>.supabase.co",
  "AnonKey": "<anon / publishable key>"
}
```

Both values are in the Supabase dashboard under **Project Settings → API**. The anon key is meant to be public, because RLS enforces access. **Never put the `service_role` key in the app.** To use a different Supabase project, run the SQL files in [database.md](database.md) on it, then replace these two values and rebuild.

## Web app on Railway

- **URL:** https://athlotrack.up.railway.app
- **Source:** GitHub `K0D1M/AthloTrack`, branch `main`. **Every push to `main` redeploys automatically.**
- **Build:** the root `Dockerfile` installs the `wasm-tools` workload on the .NET 10 SDK image and publishes `AthloTrack.Browser` in Release. The output (`wwwroot`) is copied into a `caddy:2-alpine` image.
- **Serving:** the `Caddyfile` listens on Railway's `$PORT` and serves the precompressed `.br`/`.gz` files that the publish step creates. The download is about 8 MB instead of about 28 MB. The fingerprinted `/_framework/*` files are cached as immutable. `index.html`, `main.js`, `update.js`, `version.txt` and `/_framework/dotnet.js` are always revalidated. `dotnet.js` keeps the same name on every build but lists that build's fingerprinted files, so caching it as immutable made browsers start the old app even after a reload.
- **Updates reach open apps by themselves:** the Dockerfile writes a new `version.txt` on every build. `wwwroot/update.js` remembers the version the page started with, and checks again every 5 minutes and whenever the app comes back into view.
  - Back after more than a minute away (another tab, a locked phone, the iPhone Home Screen app reopened): the page reloads itself.
  - In use: a banner «Νέα έκδοση του AthloTrack · Ανανέωση» appears, so a half-filled form isn't lost.
  - Locally there is no `version.txt`, so nothing happens.
- A Release WASM build can only be tested locally with the wasm-tools workload installed. Otherwise, push and check the Railway build logs.

Favicon, page title, web manifest and link-preview image (`og-image`) are in `AthloTrack.Browser/wwwroot/`.

### Supabase keep-alive
Supabase pauses free projects after a week without activity. A second Railway service, **`supabase-keepalive`**, runs on a cron schedule (`0 6 */3 * *`, 06:00 UTC every third day). It uses the `curlimages/curl` image and makes one request to the project's REST API with the anon key. **Don't put quotes in its start command**: Railway doesn't run it through a shell, so the quotes are passed to curl literally and break the arguments.

## Android app

Requirements: the .NET 10 SDK with `dotnet workload install android`, and the Android SDK. `AthloTrack.Android/google-services.json` (Firebase client config) is in the repo, so push works from a fresh clone. See [push-notifications.md](push-notifications.md).

| | |
|---|---|
| Package ID | `com.k0d1m.athlotrack` |
| Minimum Android | 6.0 (API 23). Push notifications need Google Play services |
| Version | `ApplicationVersion` / `ApplicationDisplayVersion` in `AthloTrack.Android.csproj` (now 13 / 1.12-alpha; the part after the dash is the release stage, shown as a tag on the login screen and a notice in Σχετικά, and dropped for a stable release). Keep `<Version>` in `AthloTrack/AthloTrack.csproj` equal to `ApplicationDisplayVersion`: it is the version shown in Σχετικά on every platform. **Increase `ApplicationVersion` for every APK you hand out**, or Android refuses to install it over the old one |

```powershell
# Debug build, installed on the connected device/emulator
dotnet build AthloTrack.Android -c Debug -t:Install

# Release APK for phones (arm64): bin/Release/net10.0-android/publish/com.k0d1m.athlotrack-Signed.apk
dotnet publish AthloTrack.Android -c Release

# Release APK for the x86_64 emulator
dotnet publish AthloTrack.Android -c Release -p:ForEmulator=true
```

### Signing key
Release builds are signed with the AthloTrack key in **`%USERPROFILE%\.athlotrack\`**, outside the repo:
- `athlotrack-release.keystore`: the key (alias `athlotrack`)
- `signing.props`: the keystore path and its password, imported by `AthloTrack.Android.csproj` for Release builds

**Back up both files somewhere safe and private** (for example an encrypted USB stick or a password manager). Every future update must be signed with this key. If it's lost, phones can't update the app: everyone would have to uninstall it and install it fresh. Never commit them (`*.keystore` / `*.jks` are git-ignored).

To build releases on another PC, copy the `.athlotrack` folder to that user's profile and adjust the keystore path in `signing.props`.

### Distributing to phones
The app isn't on the Play Store. Install the APK directly (sideloading): send the `-Signed.apk` file, open it on the phone and allow *Install unknown apps* for the app you opened it from.
- **First install of the signed release:** if the phone has a debug build from development, uninstall that first. Android won't replace an app signed with a different key.
- **Updates:** raise `ApplicationVersion`, publish, and send the new APK. It installs over the old one and keeps the user signed in.

### Emulator notes
- Use a system image with **Google Play services** (a "Google Play" image). A plain AOSP image can't receive FCM pushes.
- The emulator needs hardware acceleration. On Windows, turn on *Windows Hypervisor Platform*.

## Desktop (development)

```powershell
dotnet run --project AthloTrack.Desktop
```
The session is stored encrypted (DPAPI) in `%LOCALAPPDATA%\AthloTrack\session.bin`. Delete that file to force a fresh sign-in.
