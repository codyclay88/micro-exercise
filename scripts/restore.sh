#!/usr/bin/env bash
# Restore a gzipped pg_dump (produced by scripts/backup.sh) into the running db container.
# This OVERWRITES current data (the dump is --clean/--if-exists).
#
#   bash scripts/restore.sh path/to/microburst-YYYYMMDDTHHMMSSZ.sql.gz
set -euo pipefail
cd "$(dirname "$0")/.."
set -a; [ -f .env ] && . ./.env; set +a

FILE="${1:?usage: restore.sh <dump.sql.gz>}"
[ -f "$FILE" ] || { echo "No such file: $FILE" >&2; exit 1; }

echo "Restoring '${FILE}' into the database — this overwrites current data."
gunzip -c "$FILE" | docker compose exec -T db sh -c \
  'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' > /dev/null
echo "Restore complete."
