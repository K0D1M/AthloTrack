# AthloTrack web app (Avalonia WASM) for Railway.
# Stage 1 builds the browser head; stage 2 serves the static files with Caddy.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# wasm-tools = Emscripten toolchain, needed to link SkiaSharp's native WASM libs (WasmBuildNative).
# Python is required by Emscripten.
RUN apt-get update && apt-get install -y --no-install-recommends python3 && rm -rf /var/lib/apt/lists/* \
 && dotnet workload install wasm-tools
WORKDIR /src
COPY . .
# Only the browser head: Android/WinUI need other SDKs and aren't part of the site.
RUN dotnet publish AthloTrack.Browser/AthloTrack.Browser.csproj -c Release -o /out
# A new value on every build: the open app compares it to notice a new deploy (wwwroot/update.js).
RUN date -u +%Y%m%d%H%M%S > /out/wwwroot/version.txt

FROM caddy:2-alpine
COPY Caddyfile /etc/caddy/Caddyfile
COPY --from=build /out/wwwroot /srv
