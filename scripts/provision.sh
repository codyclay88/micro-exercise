#!/usr/bin/env bash
# Provision the DigitalOcean infrastructure for Micro-Burst with doctl:
#   - a Droplet with Docker preinstalled (Marketplace image)
#   - a cloud firewall allowing inbound 22 / 80 / 443
#   - a Block Storage volume (ext4), created and attached
# Idempotent: re-running skips resources that already exist (matched by name). Prints the
# Droplet's public IP for your DNS A record.
#
# Prereqs:
#   - doctl installed and authenticated:  doctl auth init
#   - at least one SSH key in your DO account (doctl compute ssh-key list)
# Usage:
#   bash scripts/provision.sh
# Optional config (env vars, with defaults):
#   NAME=microburst  REGION=nyc3  SIZE=s-1vcpu-1gb  VOLUME_SIZE=10  IMAGE=<slug>  SSH_KEY=<id>
set -euo pipefail

NAME="${NAME:-microburst}"
REGION="${REGION:-nyc3}"
SIZE="${SIZE:-s-1vcpu-1gb}"            # 1 GB / 1 vCPU, ~$6/mo
VOLUME_NAME="${VOLUME_NAME:-${NAME}-data}"
VOLUME_SIZE="${VOLUME_SIZE:-10}"       # GiB
FW_NAME="${FW_NAME:-${NAME}-fw}"

command -v doctl >/dev/null 2>&1 || { echo "ERROR: doctl not found — install it first." >&2; exit 1; }
doctl account get >/dev/null 2>&1 || { echo "ERROR: doctl not authenticated — run: doctl auth init" >&2; exit 1; }

# --- SSH key (the Droplet needs one so you can log in) ---
if [ -z "${SSH_KEY:-}" ]; then
  mapfile -t KEYS < <(doctl compute ssh-key list --format ID,Name --no-header)
  if [ "${#KEYS[@]}" -eq 1 ]; then
    SSH_KEY="$(awk '{print $1}' <<<"${KEYS[0]}")"
    echo "Using the only SSH key in your account: ${KEYS[0]}"
  else
    echo "Set SSH_KEY=<id> and re-run. Keys in your DO account:" >&2
    printf '  %s\n' "${KEYS[@]}" >&2
    [ "${#KEYS[@]}" -eq 0 ] && echo "  (none — add one: doctl compute ssh-key import <name> --public-key-file ~/.ssh/id_ed25519.pub)" >&2
    exit 1
  fi
fi

# --- Docker Marketplace image slug (auto-discovered; override with IMAGE=) ---
IMAGE="${IMAGE:-$(doctl compute image list-application --format Slug,Name --no-header | awk 'tolower($0) ~ /docker/ {print $1; exit}')}"
[ -n "$IMAGE" ] || { echo "ERROR: no Docker Marketplace image found; set IMAGE=<slug> (see: doctl compute image list-application)" >&2; exit 1; }
echo "Using image slug: $IMAGE"

# --- Droplet ---
if doctl compute droplet list --format Name --no-header | grep -qx "$NAME"; then
  echo "Droplet '$NAME' already exists — skipping create."
else
  echo "Creating Droplet '$NAME' ($SIZE, $REGION, image $IMAGE)…"
  doctl compute droplet create "$NAME" \
    --region "$REGION" --size "$SIZE" --image "$IMAGE" \
    --ssh-keys "$SSH_KEY" --wait
fi
DROPLET_ID="$(doctl compute droplet list --format ID,Name --no-header | awk -v n="$NAME" '$2==n{print $1; exit}')"
DROPLET_IP="$(doctl compute droplet get "$DROPLET_ID" --format PublicIPv4 --no-header)"

# --- Firewall (22/80/443 in; all out) ---
if doctl compute firewall list --format Name --no-header | grep -qx "$FW_NAME"; then
  echo "Firewall '$FW_NAME' already exists — skipping create."
  FW_ID="$(doctl compute firewall list --format ID,Name --no-header | awk -v n="$FW_NAME" '$2==n{print $1; exit}')"
else
  echo "Creating firewall '$FW_NAME' (inbound 22/80/443)…"
  FW_ID="$(doctl compute firewall create --name "$FW_NAME" \
    --inbound-rules "protocol:tcp,ports:22,address:0.0.0.0/0,address:::/0 protocol:tcp,ports:80,address:0.0.0.0/0,address:::/0 protocol:tcp,ports:443,address:0.0.0.0/0,address:::/0" \
    --outbound-rules "protocol:tcp,ports:all,address:0.0.0.0/0,address:::/0 protocol:udp,ports:all,address:0.0.0.0/0,address:::/0 protocol:icmp,address:0.0.0.0/0,address:::/0" \
    --format ID --no-header)"
fi
echo "Applying firewall to the Droplet…"
doctl compute firewall add-droplets "$FW_ID" --droplet-ids "$DROPLET_ID" >/dev/null

# --- Block Storage volume (ext4), created + attached ---
if doctl compute volume list --format Name --no-header | grep -qx "$VOLUME_NAME"; then
  echo "Volume '$VOLUME_NAME' already exists — skipping create."
  VOL_ID="$(doctl compute volume list --format ID,Name --no-header | awk -v n="$VOLUME_NAME" '$2==n{print $1; exit}')"
else
  echo "Creating volume '$VOLUME_NAME' (${VOLUME_SIZE}GiB, ext4)…"
  VOL_ID="$(doctl compute volume create "$VOLUME_NAME" --region "$REGION" --size "${VOLUME_SIZE}GiB" --fs-type ext4 --format ID --no-header)"
fi
echo "Attaching volume to the Droplet…"
doctl compute volume-action attach "$VOL_ID" "$DROPLET_ID" --wait >/dev/null

cat <<EOF

==================================================================
  Provisioned ✓
    Droplet : $NAME  ->  $DROPLET_IP
    Volume  : $VOLUME_NAME (${VOLUME_SIZE}GiB ext4), attached
    Firewall: $FW_NAME (inbound 22/80/443)

  Next steps:
    1) DNS (Hostinger): add an A record  exercise -> $DROPLET_IP
    2) ssh root@$DROPLET_IP
    3) On the Droplet, follow docs/DEPLOYMENT.md:
         - confirm the volume is mounted (DO auto-mounts ext4 volumes at /mnt/$VOLUME_NAME)
         - add swap, git clone, cp .env.example .env (set DATA_DIR=/mnt/$VOLUME_NAME +
           POSTGRES_PASSWORD + APP_DOMAIN), then: docker compose up -d --build
==================================================================
EOF
