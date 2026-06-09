#!/usr/bin/env bash
# Back up the Micro-Burst PostgreSQL database: a gzipped pg_dump written under
# $DATA_DIR/backups (rotated to the most recent $BACKUP_KEEP), and — if SPACES_BUCKET is set —
# uploaded off-site to DigitalOcean Spaces (S3-compatible).
#
# Run from anywhere; it cd's to the repo root. The stack must be up (`docker compose up -d`).
# Schedule via cron on the Droplet, e.g. daily at 03:00 UTC:
#   0 3 * * *  cd /opt/micro-exercise && bash scripts/backup.sh >> /var/log/microburst-backup.log 2>&1
set -euo pipefail
cd "$(dirname "$0")/.."

# Load .env (POSTGRES_*, DATA_DIR, SPACES_*, AWS_* for the upload).
set -a; [ -f .env ] && . ./.env; set +a

DATA_DIR="${DATA_DIR:-./data}"
BACKUP_DIR="${DATA_DIR}/backups"
KEEP="${BACKUP_KEEP:-14}"
mkdir -p "$BACKUP_DIR"

STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
FILE="microburst-${STAMP}.sql.gz"
DEST="${BACKUP_DIR}/${FILE}"

echo "[$(date -u +%H:%M:%S)] dumping database -> ${DEST}"
# Dump inside the db container: uses the container's own POSTGRES_* env and local-socket
# trust auth, so no credentials are needed here. --clean/--if-exists make the dump restorable
# over an existing database.
docker compose exec -T db sh -c \
  'pg_dump --clean --if-exists -U "$POSTGRES_USER" "$POSTGRES_DB"' | gzip > "$DEST"

if [ ! -s "$DEST" ]; then
  echo "ERROR: dump is empty — is the db container running?" >&2
  rm -f "$DEST"
  exit 1
fi

# Rotate: keep the newest $KEEP dumps locally.
ls -1t "${BACKUP_DIR}"/microburst-*.sql.gz 2>/dev/null | tail -n +"$((KEEP + 1))" | xargs -r rm -f

# Off-site upload to DigitalOcean Spaces, if configured. A throwaway aws-cli container keeps the
# host free of extra tooling; Spaces is S3-compatible via --endpoint-url.
if [ -n "${SPACES_BUCKET:-}" ]; then
  echo "[$(date -u +%H:%M:%S)] uploading -> s3://${SPACES_BUCKET}/postgres/${FILE}"
  docker run --rm \
    -e AWS_ACCESS_KEY_ID -e AWS_SECRET_ACCESS_KEY \
    -e AWS_DEFAULT_REGION="${SPACES_REGION:-us-east-1}" \
    -v "$(cd "$BACKUP_DIR" && pwd):/b:ro" \
    amazon/aws-cli s3 cp "/b/${FILE}" "s3://${SPACES_BUCKET}/postgres/${FILE}" \
      --endpoint-url "${SPACES_ENDPOINT}"
fi

echo "[$(date -u +%H:%M:%S)] backup complete: ${FILE} ($(du -h "$DEST" | cut -f1))"
