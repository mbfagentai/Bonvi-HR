# ============================================================
#  Bonvi HR Web — production image
#  Natija: statik fayllar + Caddy (avtomatik HTTPS)
#
#  Ichida IKKI alohida build:
#    · `build`       — HR ilovasi (`web/`), `/srv` ga;
#    · `ilova-build` — xodim ilovasi (PWA, `app/`), `/srv/ilova` ga.
#  Ikkalasini bitta Caddy beradi (`Caddyfile` dagi `/ilova/*` bloki).
# ============================================================

FROM node:24-alpine AS build
WORKDIR /app

RUN corepack enable

# Avval faqat bog'liqlik manifesti — pnpm qatlami keshlanadi va
# manba kodi o'zgarganda qayta o'rnatilmaydi
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile

COPY . .

# Production'da API shu domenning /api yo'lida turadi (Caddy proxy qiladi)
ARG VITE_API_URL=""
ENV VITE_API_URL=${VITE_API_URL}

# Onlayn anketaning qisqa manzili (`https://anketa.bonvi.uz`) — HR'ning
# «Anketa havolasi» oynasi uni ko'rsatadi. Bo'sh — `<sayt>/anketa`.
ARG VITE_ANKETA_URL=""
ENV VITE_ANKETA_URL=${VITE_ANKETA_URL}

RUN pnpm build

# ------------------------------------------------------------
#  Xodim ilovasi (PWA) — `app/` papkasidan, HR build'iga TEGMAYDI.
#
#  ⚠️ Manba `ilova-src` NOMLI build konteksti: image konteksti `./web`
#  bo'lib qoladi (HR build'i avvalgidek), `app/` esa docker-compose'dagi
#  `additional_contexts: { ilova-src: ./app }` orqali beriladi. Kontekst
#  berilmasa (masalan `docker build web/`), BuildKit `ilova-src` degan
#  image'ni qidirib, build xato bilan to'xtaydi — jimgina bo'sh ilova
#  yasalmaydi.
#
#  Talab: Docker Compose ≥ 2.17 va BuildKit (Docker Engine 23+ da sukut).
# ------------------------------------------------------------
FROM node:24-alpine AS ilova-build
WORKDIR /app

RUN corepack enable

COPY --from=ilova-src package.json pnpm-lock.yaml pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile

COPY --from=ilova-src . .

# `real` — haqiqiy API (`/api/*`, shu domen). `mock` — faqat namoyish
# uchun: ilova serverga umuman murojaat qilmaydi.
ARG APP_API_MODE=real
ENV VITE_API_MODE=${APP_API_MODE}

# Bo'sh = shu domen: ilova `/api/...` ga so'raydi, uni Caddy API'ga
# uzatadi. CORS kerak emas.
ENV VITE_API_URL=""

RUN pnpm build

# ------------------------------------------------------------
FROM caddy:2.11.4-alpine AS runtime

COPY --from=build /app/dist /srv
COPY --from=ilova-build /app/dist /srv/ilova
COPY Caddyfile /etc/caddy/Caddyfile

EXPOSE 80 443
