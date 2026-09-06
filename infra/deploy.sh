#!/bin/bash
# ============================================================
#  Bonvi HR — production deploy
#
#      ssh root@<server>
#      cd /opt/bonvi-hr && ./infra/deploy.sh
#
#  Skript git'dan yangilanishni oladi, image'larni quradi va
#  konteynerlarni ko'taradi. Bazaga tegmaydi: migratsiya API
#  ishga tushishida o'zi qo'llanadi (`Bootstrap__AutoMigrate`).
#
#  ⚠️ Yangi chiqarishdan OLDIN zaxira oling — avtomatik migratsiya
#     orqaga qaytarilmaydi:  ./bonvi backup
# ============================================================
set -e
cd "$(dirname "$0")/.."
ROOT="$PWD"

C="docker compose -f docker-compose.prod.yml"
NET=bonvi-hr-prod_bonvi
NODE_IMAGE=node:24-alpine

echo "### 1/7 git pull $(date +%T)"
git pull --ff-only

# NEGA KETMA-KET: prod server 2 GB RAM. Compose sukut bo'yicha
# image'larni PARALLEL quradi va .NET bilan Vite bir vaqtda
# ishlaganda build OOM bilan yiqiladi.
echo "### 2/7 api build $(date +%T)"
$C build api
echo "### 3/7 bot build $(date +%T)"
$C build bot

echo "### 4/7 backend up $(date +%T)"
$C up -d postgres redis api

# ----------------------------------------------------------
#  TypeScript klient — web build'dan OLDIN
#
#  `web/src/shared/api/generated/` git'ga tushmaydi (ataylab:
#  u har doim backend'dan qayta hosil qilinadi), ya'ni toza
#  clone'da u YO'Q va `pnpm build` «Cannot find module» bilan
#  yiqiladi. Shu sababli deploy uni o'zi generatsiya qiladi.
#
#  NEGA VAQTINCHALIK IKKINCHI API: OpenAPI hujjati ataylab faqat
#  Development'da ochiq (Program.cs) — production API uni bermaydi
#  va Orval o'qiy olmaydi. Bu konteyner PORT OCHMAYDI, faqat ichki
#  tarmoqda turadi va ishi tugashi bilan o'chiriladi.
# ----------------------------------------------------------
echo "### 5/7 klient generatsiyasi $(date +%T)"
docker rm -f api-openapi >/dev/null 2>&1 || true
$C run -d --rm --no-deps --name api-openapi -e ASPNETCORE_ENVIRONMENT=Development api >/dev/null
until docker run --rm --network "$NET" curlimages/curl:latest \
        -s -o /dev/null -w "%{http_code}" http://api-openapi:8080/openapi/v1.json | grep -q 200; do
  sleep 3
done
docker run --rm \
  -v "$ROOT/web:/app" \
  -v bonvi-prod-node-modules:/app/node_modules \
  -v bonvi-prod-pnpm-store:/root/.local/share/pnpm/store \
  -w /app \
  -e VITE_OPENAPI_URL=http://api-openapi:8080/openapi/v1.json \
  --network "$NET" \
  "$NODE_IMAGE" sh -c "corepack enable >/dev/null 2>&1; pnpm install --frozen-lockfile && pnpm gen"
docker rm -f api-openapi >/dev/null 2>&1 || true

echo "### 6/7 web build $(date +%T)"
$C build web

echo "### 7/7 up $(date +%T)"
$C up -d

echo "### TAYYOR $(date +%T)"
$C ps
