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
- **Serving:** the `Caddyfile` listens on Railway's `$PORT` and serves the precompressed `.br`/`.gz` files that the publish step creates. The download is about 8 MB instead of about 28 MB. `/_framework/*` is cached as immutable; `index.html` and `main.js` are always revalidated, so browsers pick up a new deploy.
- A Release WASM build can only be tested locally with the wasm-tools workload installed. Otherwise, push and check the Railway build logs.

Favicon, page title, web manifest and link-preview image (`og-image`) are in `AthloTrack.Browser/wwwroot/`.

### Supabase keep-alive
Supabase pauses free projects after a week without activity. A second Railway service, **`supabase-keepalive`**, runs on a cron schedule (`0 6 */3 * *`, 06:00 UTC every third day). It uses the `curlimages/curl` image and makes one request to the project's REST API with the anon key. **Don't put quotes in its start command**: Railway doesn't run it through a shell, so the quotes are passed to curl literally and break the arguments.

## Android app

Requirements: the .NET 10 SDK with `dotnet workload install android`, and the Android SDK. For push, you also need `AthloTrack.Android/google-services.json`, see [push-notifications.md](push-notifications.md).

| | |
|---|---|
| Package ID | `com.k0d1m.athlotrack` |
| Minimum Android | 6.0 (API 23). Push notifications need Google Play services |
| Version | `ApplicationVersion` / `ApplicationDisplayVersion` in `AthloTrack.Android.csproj`. **Increase `ApplicationVersion` for every release**, or Android refuses to install the new APK over the old one |

```powershell
# Debug build, installed on the connected device/emulator
dotnet build AthloTrack.Android -c Debug -t:Install

# Release APK (the *-Signed.apk in bin/Release/net10.0-android/publish/)
dotnet publish AthloTrack.Android -c Release
```

### Distributing to phones
The app isn't on the Play Store. Install the APK directly (sideloading): send the file, open it on the phone and allow *Install unknown apps* for the app you opened it from.

For real users, sign the release with **your own keystore**:
```powershell
keytool -genkeypair -v -keystore athlotrack.keystore -alias athlotrack -keyalg RSA -keysize 2048 -validity 10000
dotnet publish AthloTrack.Android -c Release `
  -p:AndroidKeyStore=true -p:AndroidSigningKeyStore=athlotrack.keystore `
  -p:AndroidSigningKeyAlias=athlotrack -p:AndroidSigningKeyPass=<pass> -p:AndroidSigningStorePass=<pass>
```
Keep the keystore and its password safe and **out of git** (`*.keystore` / `*.jks`). Every future update must be signed with the same key, or phones won't accept it.

### Emulator notes
- Use a system image with **Google Play services** (a "Google Play" image). A plain AOSP image can't receive FCM pushes.
- The emulator needs hardware acceleration. On Windows, turn on *Windows Hypervisor Platform*.

## Desktop (development)

```powershell
dotnet run --project AthloTrack.Desktop
```
The session is stored encrypted (DPAPI) in `%LOCALAPPDATA%\AthloTrack\session.bin`. Delete that file to force a fresh sign-in.
