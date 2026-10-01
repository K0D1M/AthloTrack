#!/bin/sh
# AthloTrack nightly job: keep the free Supabase project awake, then back up the database and
# the profile photos into the Railway bucket. Variables: see docs/backups.md.
set -eu

STAMP=$(date -u +%Y-%m-%d_%H%M)
KEEP_DAYS="${KEEP_DAYS:-30}"
WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT

# rclone remotes from environment variables: "backup" = Railway bucket, "supabase" = Supabase Storage (S3).
export RCLONE_CONFIG_BACKUP_TYPE=s3 RCLONE_CONFIG_BACKUP_PROVIDER=Other
export RCLONE_CONFIG_BACKUP_ENDPOINT="$BACKUP_S3_ENDPOINT" RCLONE_CONFIG_BACKUP_REGION="$BACKUP_S3_REGION"
export RCLONE_CONFIG_BACKUP_ACCESS_KEY_ID="$BACKUP_S3_ACCESS_KEY_ID" RCLONE_CONFIG_BACKUP_SECRET_ACCESS_KEY="$BACKUP_S3_SECRET_ACCESS_KEY"
export RCLONE_CONFIG_SUPABASE_TYPE=s3 RCLONE_CONFIG_SUPABASE_PROVIDER=Other RCLONE_CONFIG_SUPABASE_FORCE_PATH_STYLE=true
export RCLONE_CONFIG_SUPABASE_ENDPOINT="$SUPABASE_S3_ENDPOINT" RCLONE_CONFIG_SUPABASE_REGION="$SUPABASE_S3_REGION"
export RCLONE_CONFIG_SUPABASE_ACCESS_KEY_ID="$SUPABASE_S3_ACCESS_KEY_ID" RCLONE_CONFIG_SUPABASE_SECRET_ACCESS_KEY="$SUPABASE_S3_SECRET_ACCESS_KEY"

# 1. Keep-alive: a real API request, so Supabase doesn't pause the free project after a quiet week.
code=$(curl -sS -o /dev/null -w '%{http_code}' -H "apikey: $SUPABASE_ANON_KEY" "$SUPABASE_URL/rest/v1/coaches?select=id")
echo "keep-alive: HTTP $code"

# 2. Database. The schema is also in git (supabase/*.sql); these dumps hold the data.
#    public = AthloTrack's tables (+ functions/policies); auth users + identities = the logins.
pg_dump "$SUPABASE_DB_URL" --schema=public --no-owner --no-privileges | gzip > "$WORK/public.sql.gz"
pg_dump "$SUPABASE_DB_URL" --data-only --table=auth.users --table=auth.identities --no-owner | gzip > "$WORK/auth.sql.gz"
pg_dump "$SUPABASE_DB_URL" --data-only --table=storage.objects --no-owner | gzip > "$WORK/storage-objects.sql.gz"
rclone copy "$WORK" "backup:$BACKUP_S3_BUCKET/db/$STAMP"
echo "database: db/$STAMP ($(du -ch "$WORK"/*.gz | tail -1 | cut -f1))"

# 3. Photos: mirror of the private "avatars" bucket.
rclone sync "supabase:avatars" "backup:$BACKUP_S3_BUCKET/photos/avatars"
echo "photos: $(rclone size "backup:$BACKUP_S3_BUCKET/photos/avatars" | tr '\n' ' ')"

# 4. Keep the last $KEEP_DAYS days of database dumps.
rclone delete --min-age "${KEEP_DAYS}d" "backup:$BACKUP_S3_BUCKET/db"
rclone rmdirs --leave-root "backup:$BACKUP_S3_BUCKET/db"
echo "backup done"
