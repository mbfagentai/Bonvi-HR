# ============================================================
#  Bonvi HR Web — production image
#  Natija: statik fayllar + Caddy (avtomatik HTTPS)
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

RUN pnpm build

# ------------------------------------------------------------
FROM caddy:2.11.4-alpine AS runtime

COPY --from=build /app/dist /srv
COPY Caddyfile /etc/caddy/Caddyfile

EXPOSE 80 443
