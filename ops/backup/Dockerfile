# AthloTrack nightly job on Railway (cron): Supabase keep-alive + database and photo backups
# into a private Railway bucket. See docs/backups.md. Railway service settings: build from the
# repo root, Dockerfile path "ops/backup/Dockerfile", watch path "/ops/backup/**",
# cron "30 1 * * *", restart policy NEVER.
FROM alpine:3.22
# pg_dump 17 can dump older servers too; rclone talks to both S3 endpoints.
RUN apk add --no-cache postgresql17-client rclone curl ca-certificates tzdata \
 && mkdir -p /root/.config/rclone && touch /root/.config/rclone/rclone.conf
# (empty rclone.conf: the remotes come from environment variables; this silences "config not found")
COPY ops/backup/backup.sh /backup.sh
CMD ["/bin/sh", "/backup.sh"]
