#!/bin/sh
# ============================================================
#  BONVI HR — zaxiradan tiklash
#
#  `./bonvi restore <fayl>` orqali chaqiriladi. Har doim `postgres`
#  konteyneri ICHIDA, local unix socket orqali ishlaydi — parol kerak
#  emas (backup.sh dagi izohga qarang). Tasdiqlash `./bonvi` skriptining
#  o'zida — bu skript ishga tushgan payt qaror allaqachon qabul qilingan.
#
#  Ishlatish (konteyner ichida): restore.sh /backups/nomi.dump
# ============================================================
set -eu

FILE="${1:?fayl nomi kerak: restore.sh /backups/nomi.dump}"
[ -f "$FILE" ] || { echo "[restore] XATO: fayl topilmadi: $FILE" >&2; exit 1; }

: "${PGDATABASE:=${POSTGRES_DB:?POSTGRES_DB yoki PGDATABASE kerak}}"
: "${PGUSER:=${POSTGRES_USER:-postgres}}"

log() { echo "[restore $(date '+%Y-%m-%d %H:%M:%S')] $*"; }

log "tekshirilmoqda: $FILE"
if ! pg_restore --list "$FILE" >/dev/null 2>&1; then
  log "XATO: fayl buzilgan yoki pg_dump arxivi emas — tiklanmadi"
  exit 1
fi

log "tiklanmoqda: $PGDATABASE ← $FILE"
# --clean --if-exists: tiklashdan oldin mavjud obyektlarni o'chiradi, ya'ni
# bazadagi HOZIRGI ma'lumot arxivdagisi bilan almashtiriladi (ataylab —
# bu buyruqning o'zi xavfli amal deb hujjatlashtirilgan).
# --no-owner --no-privileges: rol nomlari muhitlar orasida farq qilishi
# mumkin (masalan .env'da POSTGRES_USER o'zgargan bo'lsa) — egalik/huquq
# buyruqlari muvaffaqiyatsizlikka olib kelmasin.
pg_restore -U "$PGUSER" -d "$PGDATABASE" --clean --if-exists --no-owner --no-privileges "$FILE"
log "tugadi"
