# Backups and keep-alive

The free Supabase plan has no downloadable backups, and it **pauses a project after about a week without activity**. A nightly Railway job takes care of both.

## The `supabase-backup` job

Railway service **`supabase-backup`** (project AthloTrack). It's built from [`ops/backup/`](../ops/backup) and runs on a cron schedule, **`30 1 * * *`** (01:30 UTC = 03:30/04:30 Greek time). Each run:

1. **Keep-alive:** one REST request to Supabase, so the project counts as active.
2. **Photos:** a mirror of the private `avatars` storage bucket into `photos/avatars/`.
3. **Database:** three `pg_dump` files, gzipped, into `db/<date>_<time>/` in the bucket:
   - `public.sql.gz`: AthloTrack's tables with all data, plus functions and policies
   - `auth.sql.gz`: the logins (`auth.users`, `auth.identities`), so accounts and passwords survive a restore
   - `storage-objects.sql.gz`: the storage file index for the photos
4. **Retention:** only after a fully successful run, database dumps older than `KEEP_DAYS` (30) are deleted. The photo mirror always matches the live bucket.

Backups go to the private Railway bucket **`athlotrack-backups`** (region Amsterdam). The cost is a few cents a month for this size.

### Variables (Railway â†’ `supabase-backup` â†’ Variables)

| Variable | Value |
|---|---|
| `SUPABASE_URL`, `SUPABASE_ANON_KEY` | Project URL and public key (same as the app) |
| `SUPABASE_DB_URL` | **Secret.** Supabase â†’ **Connect â†’ Session pooler** URI, with the database password filled in |
| `SUPABASE_S3_ENDPOINT` | `https://tahsfrptizcdqhvghzqs.supabase.co/storage/v1/s3` |
| `SUPABASE_S3_REGION` | Supabase â†’ Project Settings â†’ Storage â†’ S3 Connection â†’ Region |
| `SUPABASE_S3_ACCESS_KEY_ID`, `SUPABASE_S3_SECRET_ACCESS_KEY` | **Secret.** Same page â†’ S3 Access Keys |
| `BACKUP_S3_*` | References to the `athlotrack-backups` bucket (`${{athlotrack-backups.ENDPOINT}}` etc.) |
| `KEEP_DAYS` | Days of database dumps to keep (30) |

If you change the database password in Supabase, update `SUPABASE_DB_URL` here too, or backups stop. The keep-alive still runs, because it is the first step.

### Checking it works
Open **Railway â†’ `supabase-backup` â†’ Deployments â†’ latest â†’ Logs**. A good run ends with:
```
keep-alive: HTTP 200
database: db/2026-10-02_0130 (â€¦)
photos: Total objects: â€¦ Total size: â€¦
backup done
```
To browse or download the files, use **Railway â†’ `athlotrack-backups` â†’ Files**.

## Restoring

Only if the Supabase project is lost or the data is damaged:

1. Create a new Supabase project (or use the existing one), and run the files in `supabase/` in order (see [database.md](database.md)). That gives you the schema, triggers and policies.
2. Download the latest `db/<date>/` files from the bucket and unzip them.
3. In a terminal with the PostgreSQL client and the new project's connection string:
   ```bash
   psql "$DB_URL" -f auth.sql            # logins first: athletes/coaches reference them
   psql "$DB_URL" -c "truncate public.notifications, public.workout_programs, public.measurements, public.athletes, public.coaches cascade"
   psql "$DB_URL" -f public.sql          # if objects already exist, ignore "already exists" errors: the data still loads
   psql "$DB_URL" -f storage-objects.sql
   ```
4. Upload the files from `photos/avatars/` back into the `avatars` storage bucket, keeping the folder names (athlete/coach ids).
5. If the project URL or key changed, update `AthloTrack/Assets/supabase.config.json`, the push function's webhook, and this job's variables.
