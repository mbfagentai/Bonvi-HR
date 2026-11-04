#!/usr/bin/env bash
# Deploy from GitHub on the Bonvi VM (bonvi-srv) — docker-compose.server.yml on top of the prod stack.
#
#   /opt/bonvi-hr/infra/deploy-server.sh            # origin/main
#   /opt/bonvi-hr/infra/deploy-server.sh <branch>
#
# Same steps as infra/deploy.sh (the eskiz VPS): build, backend up and wait until migrations finish,
# generate the TypeScript client from a temporary Development API (the generated folder is not in git),
# build the web image, bring everything up. Server-only state is never in git and is left alone:
# .env and cloudflared/. Profiles (`bot`, `tunnel`, `isup`) come from COMPOSE_PROFILES in .env.
set -euo pipefail
cd "$(dirname "$0")/.."
ROOT="$PWD"

branch="${1:-main}"
git fetch --quiet origin "$branch"
git checkout --quiet "$branch"
git merge --ff-only --quiet "origin/$branch"
echo "deploying $(git log -1 --format='%h %s')"

C=(docker compose -p bonvi-hr -f docker-compose.prod.yml -f docker-compose.server.yml --env-file .env)
NET=bonvi-hr_bonvi
NODE_IMAGE=node:24-alpine
DATA=/srv/data/bonvi-hr

# The data disk must be mounted — otherwise files would land under the empty (immutable) mount point.
mountpoint -q /srv/data || { echo "/srv/data is not mounted" >&2; exit 1; }
mkdir -p "$DATA/backups" "$DATA/uploads" "$DATA/logs/caddy"
chown 2000:2000 "$DATA/uploads" "$DATA/logs"
chmod 700 "$DATA"

echo "### build $(date +%T)"
"${C[@]}" build api bot

# Bot is not named: it starts at the end only when its profile is on (docker-compose.server.yml).
echo "### backend up $(date +%T)"
"${C[@]}" up -d postgres redis api
until docker run --rm --network "$NET" curlimages/curl:latest \
        -s -o /dev/null -w "%{http_code}" http://api:8080/health/ready | grep -q 200; do
  sleep 3
done

echo "### client generation $(date +%T)"
docker rm -f bonvi-hr-api-openapi >/dev/null 2>&1 || true
"${C[@]}" run -d --rm --no-deps --name bonvi-hr-api-openapi -e ASPNETCORE_ENVIRONMENT=Development api >/dev/null
until docker run --rm --network "$NET" curlimages/curl:latest \
        -s -o /dev/null -w "%{http_code}" http://bonvi-hr-api-openapi:8080/openapi/v1.json | grep -q 200; do
  sleep 3
done
docker run --rm \
  -v "$ROOT/web:/app" \
  -v bonvi-hr-node-modules:/app/node_modules \
  -v bonvi-hr-pnpm-store:/root/.local/share/pnpm/store \
  -w /app \
  -e VITE_OPENAPI_URL=http://bonvi-hr-api-openapi:8080/openapi/v1.json \
  --network "$NET" \
  "$NODE_IMAGE" sh -c "corepack enable >/dev/null 2>&1; pnpm install --frozen-lockfile && pnpm gen"
docker rm -f bonvi-hr-api-openapi >/dev/null 2>&1 || true

echo "### web build $(date +%T)"
"${C[@]}" build web

echo "### up $(date +%T)"
"${C[@]}" up -d
"${C[@]}" ps --format '{{.Service}}\t{{.State}}\t{{.Status}}'

# Through Caddy, with the site's own Host header (the site address is `http://<host>` in .env).
site=$(grep -E '^SITE_ADDRESS=' .env | cut -d= -f2- | sed -E 's#^https?://##; s#,.*##')
for _ in $(seq 1 30); do
  if curl -fsS -o /dev/null -H "Host: $site" http://127.0.0.1:8320/health/ready; then
    echo "health: ok ($site)"
    exit 0
  fi
  sleep 2
done
echo "health: FAILED — check: ${C[*]} logs --tail 50 api web" >&2
exit 1
