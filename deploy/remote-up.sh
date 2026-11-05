#!/usr/bin/env bash
# Starts or upgrades Rentilo on the server. Run from ~/rentilo (deploy.ps1 / deploy.sh call it for you).
#   bash remote-up.sh                 start/upgrade using the image already loaded
#   INIT_SITE_ADDRESS=example.com ... only used when .env doesn't exist yet
set -euo pipefail
cd "$(dirname "$0")"

if [[ ! -f .env ]]; then
  echo "==> First deploy: creating .env"
  cp .env.example .env
  password=$(head -c 32 /dev/urandom | base64 | tr -dc 'A-Za-z0-9' | head -c 32)
  sed -i "s|^POSTGRES_PASSWORD=.*|POSTGRES_PASSWORD=${password}|" .env
  [[ -n "${INIT_SITE_ADDRESS:-}" ]] && sed -i "s|^SITE_ADDRESS=.*|SITE_ADDRESS=${INIT_SITE_ADDRESS}|" .env
  [[ -n "${INIT_SEED_DEMO_DATA:-}" ]] && sed -i "s|^SEED_DEMO_DATA=.*|SEED_DEMO_DATA=${INIT_SEED_DEMO_DATA}|" .env
  chmod 600 .env
fi

if [[ -f rentilo-image.tar ]]; then
  echo "==> Loading the new app image"
  docker load -i rentilo-image.tar
  rm -f rentilo-image.tar
fi

mkdir -p backups
echo "==> Starting containers"
docker compose -f docker-compose.prod.yml up -d --remove-orphans

echo "==> Waiting for the app to become healthy"
for _ in $(seq 1 60); do
  if docker compose -f docker-compose.prod.yml exec -T caddy wget -qO- http://web:8080/healthz 2>/dev/null | grep -q Healthy; then
    echo "    Rentilo is healthy."
    docker image prune -f >/dev/null
    site=$(grep '^SITE_ADDRESS=' .env | cut -d= -f2)
    if [[ "$site" == :* ]]; then
      echo "    Open: http://$(curl -s --max-time 5 https://ifconfig.me || hostname -I | awk '{print $1}')${site#:80}"
    else
      echo "    Open: https://$site"
    fi
    exit 0
  fi
  sleep 2
done
echo "The app did not become healthy. Recent logs:" >&2
docker compose -f docker-compose.prod.yml logs --tail=60 web >&2
exit 1
