# Deployment — Railway (managed)

The low-ops option: [Railway](https://railway.app) builds the repo's `Dockerfile`, runs the
container, provides **managed PostgreSQL**, terminates **TLS**, and **auto-deploys on every push
to `main`**. No server to patch, secure, or back up by hand.

> Prefer to self-host on a VM instead? See [`DEPLOYMENT.md`](DEPLOYMENT.md) (DigitalOcean Droplet
> + Docker Compose + Caddy). The same `Dockerfile` powers both — the app is platform-agnostic.

```
GitHub (push to main) ──► Railway build (Dockerfile) ──► app service ──► Postgres (managed)
                                       │                     │
                                  auto TLS + domain     Volume mounted at /keys
                                                        (Data Protection keys)
```

Railway has **no free tier** and bills by usage (subscription + metered resources), so an
always-on app + small Postgres is roughly **$5–20/mo** depending on usage — check current pricing.

---

## 1. Create the project

1. Railway → **New Project → Deploy from GitHub repo** → `codyclay88/micro-exercise`, branch
   `main`. Railway detects the `Dockerfile` and `railway.json` (Dockerfile build, health check at
   `/healthz`) and builds automatically. It redeploys on every push to `main`.

## 2. Add managed Postgres

2. In the project: **New → Database → PostgreSQL**. This creates a `Postgres` service with
   `PG*` connection variables on Railway's private network.

## 3. Wire the app to the database

3. On the **app** service → **Variables**, add (Railway resolves the `${{Postgres.*}}` references):
   ```
   ConnectionStrings__AppDb = Host=${{Postgres.PGHOST}};Port=${{Postgres.PGPORT}};Database=${{Postgres.PGDATABASE}};Username=${{Postgres.PGUSER}};Password=${{Postgres.PGPASSWORD}};SSL Mode=Prefer;Trust Server Certificate=true
   ASPNETCORE_ENVIRONMENT = Production
   ```
   - `ASPNETCORE_ENVIRONMENT=Production` turns on Data-Protection-key persistence (`/keys`) and
     Secure cookies behind the proxy.
   - `SSL Mode=Prefer` works for both Railway's private and public networking.
   - You do **not** set `PORT` — Railway injects it, and the app binds it automatically.

## 4. Persist Data Protection keys

4. App service → **Volumes** → add a small (1 GB) volume mounted at **`/keys`**. Without it, the
   key ring is ephemeral and every redeploy logs all users out (and breaks antiforgery). The app
   already writes keys there in Production — no code change.

## 5. Custom domain

5. App service → **Settings → Networking → Custom Domain** → add `exercise.codyclay.com`. Railway
   shows a **CNAME** target. In **Hostinger DNS**, add a CNAME: `exercise` → that target.
   (This replaces the A-record from the Droplet plan.) Railway issues the TLS cert automatically
   once DNS resolves. Until then, use the `*.up.railway.app` URL Railway assigns.

## 6. Go live

6. The first deploy runs EF migrations on startup (seeds the global exercise catalog). Browse the
   domain, register, and you're live. **Redeploy = `git push origin main`** (Railway rebuilds and
   deploys; `/healthz` gates the cutover).

---

## Backups

Railway's managed Postgres includes backups (retention is plan-dependent — confirm in the DB
service's settings). If you want your own off-site dumps as well, `scripts/backup.sh` can run from
any machine with Docker against the **public** DB connection (Postgres service → Connect → public
URL) — set `COMPOSE_FILE`/`POSTGRES_*` appropriately, or adapt it to `pg_dump` the public URL.
For most personal use, Railway's built-in backups are enough.

## Rollback / logs / shell

- **Rollback:** Railway → app service → Deployments → redeploy a previous build.
- **Logs:** the app service's Deployments/Logs tab (startup, migrations, requests).
- **DB shell:** Postgres service → Connect → `psql` command, or Railway's data tab.

## Notes

- The DigitalOcean files (`compose.yaml`, `Caddyfile`, `scripts/provision.sh`,
  `scripts/backup.sh`, `scripts/restore.sh`) stay in the repo and are inert on Railway — they're
  your escape hatch back to self-hosting.
- Local dev is unchanged: `docker compose -f compose.dev.yaml up -d` + `dotnet run`.
