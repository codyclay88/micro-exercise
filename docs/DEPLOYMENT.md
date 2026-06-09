# Deployment (DigitalOcean)

Micro-Burst runs on a **single DigitalOcean Droplet** via Docker Compose: the app, PostgreSQL,
and a Caddy reverse proxy (automatic HTTPS), all on one box. Persistent state lives on an
attached **Block Storage volume**, and the database is backed up nightly (locally + off-site to
DigitalOcean Spaces).

```
Droplet (1 GB) ──────────────────────────────────────────────
  docker compose:  caddy (:80/:443, TLS)  →  app (:8080)  →  db (postgres:17)
  /mnt/microburst_data  (Block Storage volume)
     ├── postgres/      Postgres data
     ├── dpkeys/        Data Protection keys (auth cookies survive rebuilds)
     └── backups/       rotated pg_dump archives  ──nightly──►  DO Spaces
```

Cost: ~$6/mo Droplet + ~$1/mo for a 10 GB volume + a few cents for Spaces.

---

## 1. DNS (Hostinger)

`exercise.codyclay.com`'s DNS is hosted at Hostinger. Add an **A record**: host `exercise`
→ the Droplet's public IPv4. Do this early so it has propagated before step 5 (Caddy needs the
name resolving to issue the TLS cert).

## 2. Create the Droplet

- Image: **Docker on Ubuntu** (Marketplace) — Docker + Compose preinstalled.
- Size: **1 GB / 1 vCPU** ($6/mo) is the practical minimum.
- Add your **SSH key**.
- After it boots, add a **swapfile** (1 GB RAM is tight for an in-place image build + Postgres):
  ```bash
  fallocate -l 2G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile
  echo '/swapfile none swap sw 0 0' >> /etc/fstab
  ```
- Open **80, 443, 22** if you enable a firewall (cloud firewall or `ufw`).

## 3. Attach the Block Storage volume

Create a **Volume** in the DO console (e.g. 10 GB, same region as the Droplet) and attach it.
DO can auto-format (ext4) and auto-mount it; confirm the mount point (typically
`/mnt/<volume-name>`). If you need to do it manually:

```bash
# Find the device, format once (ONLY if brand-new/empty), mount, and persist in fstab.
lsblk
mkfs.ext4 -F /dev/disk/by-id/scsi-0DO_Volume_<name>     # skip if already formatted
mkdir -p /mnt/microburst_data
mount -o discard,defaults /dev/disk/by-id/scsi-0DO_Volume_<name> /mnt/microburst_data
echo '/dev/disk/by-id/scsi-0DO_Volume_<name> /mnt/microburst_data ext4 defaults,nofail,discard 0 2' >> /etc/fstab
```

This volume survives the Droplet being destroyed and can be re-attached to a replacement
Droplet — your data comes with it.

## 4. Configure

```bash
git clone https://github.com/codyclay88/micro-exercise.git /opt/micro-exercise
cd /opt/micro-exercise
cp .env.example .env
```

Edit `.env`:
- `APP_DOMAIN=exercise.codyclay.com`
- `POSTGRES_PASSWORD=` — a long random secret.
- **`DATA_DIR=/mnt/microburst_data`** — uncomment + point at the mounted volume. (If left unset,
  data goes to `./data` on the Droplet's local disk — fine, but not on the durable volume.)
- For off-site backups, set `SPACES_BUCKET`, `SPACES_ENDPOINT`, `SPACES_REGION`, and the
  `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` Spaces keys (see step 6).

## 5. Deploy

```bash
docker compose up -d --build
```

Caddy fetches the Let's Encrypt cert once DNS resolves and port 80 is reachable. Browse
`https://exercise.codyclay.com`, register, and you're live. The app applies EF migrations on
startup (seeds the global exercise catalog); user data is created on registration.

**Redeploy** (after `git push` to `main`):
```bash
git pull && docker compose up -d --build
```
Postgres data, DP keys, and TLS certs persist across redeploys (and, on the volume, rebuilds).

## 6. Backups

`scripts/backup.sh` writes a gzipped `pg_dump` to `$DATA_DIR/backups` (keeps the newest 14) and,
if `SPACES_BUCKET` is set, uploads it to DigitalOcean Spaces (S3-compatible, via a throwaway
`aws-cli` container — no host tooling needed).

**Spaces setup:** in the DO console create a **Space** (e.g. `microburst-backups`) and a
**Spaces access key** pair; put the bucket name, the region endpoint
(`https://<region>.digitaloceanspaces.com`), and the key/secret into `.env`.

**Schedule** a daily run via cron on the Droplet:
```bash
crontab -e
# 03:00 UTC daily:
0 3 * * *  cd /opt/micro-exercise && bash scripts/backup.sh >> /var/log/microburst-backup.log 2>&1
```

Run one on demand to test: `bash scripts/backup.sh`.

> Belt-and-suspenders: also enable **DO Droplet Backups** (console toggle, ~20% of Droplet cost)
> for whole-disk snapshots. The `pg_dump` archives give granular, fast, off-box restores; the
> Droplet backup covers everything else.

## 7. Restore

```bash
# From a local dump under $DATA_DIR/backups:
bash scripts/restore.sh /mnt/microburst_data/backups/microburst-20260609T030000Z.sql.gz

# From Spaces: download it first, then restore.
docker run --rm -e AWS_ACCESS_KEY_ID -e AWS_SECRET_ACCESS_KEY \
  -e AWS_DEFAULT_REGION=$SPACES_REGION -v "$PWD:/b" amazon/aws-cli \
  s3 cp s3://$SPACES_BUCKET/postgres/<file>.sql.gz /b/ --endpoint-url $SPACES_ENDPOINT
bash scripts/restore.sh <file>.sql.gz
```

Restore overwrites current data (the dump is `--clean --if-exists`). The stack must be up.

---

## Notes

- **Local dev** is unaffected: `docker compose -f compose.dev.yaml up -d` runs Postgres on host
  port 55432 with a named volume; `DATA_DIR`/Spaces are prod-only.
- **First page load** downloads the Blazor WASM runtime (one-time, cached). If you want to
  shrink it, enable Brotli precompression of the published `_framework` assets.
- The whole durable footprint is under `DATA_DIR`, so migrating to a bigger Droplet = attach the
  volume to the new box, `git clone`, set the same `.env`, `docker compose up -d --build`.
