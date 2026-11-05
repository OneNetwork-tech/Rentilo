#!/usr/bin/env bash
# Mac/Linux version of deploy.ps1: builds Rentilo with Docker on this machine and deploys over SSH.
#   ./deploy/deploy.sh [--setup] [--seed-demo-data] [--domain rentilo.example.com]
# Override the target with SERVER=user@host and SSH_KEY=/path/to/key.
set -euo pipefail

SERVER="${SERVER:-ubuntu@207.127.90.194}"
SSH_KEY="${SSH_KEY:-$HOME/.ssh/ssh-key-2025-08-28.key}"
DOMAIN="" SETUP=0 SEED=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --setup) SETUP=1 ;;
    --seed-demo-data) SEED=true ;;
    --domain) DOMAIN="$2"; shift ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
  shift
done

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DEPLOY="$ROOT/deploy"
REMOTE="rentilo"   # relative to the remote user's home directory
SSH=(ssh -i "$SSH_KEY" -o StrictHostKeyChecking=accept-new -o ServerAliveInterval=30)
SCP=(scp -i "$SSH_KEY" -o StrictHostKeyChecking=accept-new)
step() { printf '\033[36m==> %s\033[0m\n' "$*"; }

step "Connecting to $SERVER"
"${SSH[@]}" "$SERVER" "mkdir -p $REMOTE"
if [[ $SETUP == 1 ]] || [[ "$("${SSH[@]}" "$SERVER" 'command -v docker >/dev/null && echo yes || echo no')" != yes ]]; then
  step "Preparing the server (Docker, firewall, swap)"
  "${SCP[@]}" "$DEPLOY/server-setup.sh" "$SERVER:$REMOTE/"
  "${SSH[@]}" "$SERVER" "sudo bash $REMOTE/server-setup.sh"
fi

case "$("${SSH[@]}" "$SERVER" uname -m)" in
  aarch64|arm64) PLATFORM=linux/arm64 ;;
  *) PLATFORM=linux/amd64 ;;
esac

step "Building the app image for $PLATFORM"
docker buildx build --platform "$PLATFORM" -t rentilo:latest --load "$ROOT"

step "Exporting and uploading the image"
mkdir -p "$DEPLOY/out"
docker save -o "$DEPLOY/out/rentilo-image.tar" rentilo:latest
"${SCP[@]}" "$DEPLOY"/{docker-compose.prod.yml,Caddyfile,backup.sh,remote-up.sh,.env.example} "$DEPLOY/out/rentilo-image.tar" "$SERVER:$REMOTE/"

step "Starting Rentilo on the server"
"${SSH[@]}" "$SERVER" "cd $REMOTE && INIT_SITE_ADDRESS='$DOMAIN' INIT_SEED_DEMO_DATA='$SEED' bash remote-up.sh"
rm -f "$DEPLOY/out/rentilo-image.tar"
step "Done"
