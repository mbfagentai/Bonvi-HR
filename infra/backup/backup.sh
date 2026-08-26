#!/bin/sh
# ============================================================
#  BONVI HR — pg_dump zaxira
#
#  Har doim PostgreSQL konteyneri ICHIDA ishlaydi (xostda pg_dump yo'q).
#  Ikki xil muhitdan chaqiriladi va ikkalasida ham parol ochiq ko'rinmaydi:
#
#    1. `./bonvi backup` (qo'lda, dev yoki prod) — `postgres` konteyneriga
#       `docker compose exec` bilan kirib ishga tushiriladi. Bu holatda
#       ulanish local unix socket orqali — PostgreSQL rasmiy image'ida
#       socket ulanishlari standart holda `trust` (parolsiz), shu sababli
#       PGPASSWORD UMUMAN kerak emas.
#    2. Production'dagi `backup` konteyneri (docker-compose.prod.yml) —
#       alohida konteyner bo'lgani uchun tarmoq orqali ulanadi va PGHOST/
#       PGPASSWORD kerak. Ular compose'ning `environment:` blokida .env'dan
#       o'qiladi — hech qanday buyruq qatorida yozilmaydi.
#
#  Muvaffaqiyatsizlik JIMGINA o'tmasligi kerak degan talab shu skriptning
#  markazida: har bir qadam `set -e` bilan to'xtaydi, yarim yozilgan fayl
#  `.part` kengaytmasi bilan yashiriladi va faqat TO'LIQ tekshiruvdan
#  o'tgandan keyin asosiy nomga ko'chiriladi.
# ============================================================
set -eu

BACKUP_DIR="${BACKUP_DIR:-/backups}"
RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-30}"

# PGDATABASE/PGUSER libpq standart o'zgaruvchilari — pg_dump ularni
# avtomatik o'qiydi. Local exec holatida ular yo'q, shu sababli postgres
# image'ining o'z POSTGRES_DB/POSTGRES_USER'idan foydalanamiz.
: "${PGDATABASE:=${POSTGRES_DB:?POSTGRES_DB yoki PGDATABASE kerak}}"
: "${PGUSER:=${POSTGRES_USER:-postgres}}"
export PGDATABASE PGUSER

log() { echo "[backup $(date '+%Y-%m-%d %H:%M:%S')] $*"; }

mkdir -p "$BACKUP_DIR"

# BACKUP_STAMP tashqaridan berilishi mumkin — backup-files.sh bilan bir
# xil stamp ishlatish uchun (./bonvi backup va prod cron shunday qiladi).
STAMP="${BACKUP_STAMP:-$(date '+%Y-%m-%d_%H%M%S')}"
FILE="$BACKUP_DIR/bonvi_${PGDATABASE}_${STAMP}.dump"
TMP_FILE="${FILE}.part"

log "boshlandi: $PGDATABASE → $FILE"

# -Fc: PostgreSQL 'custom' formati — o'zi siqiladi (gzip'dan qo'shimcha
# ehtiyoj yo'q) va pg_restore --list bilan tuzilishini tekshirish imkonini
# beradi. .part kengaytmasi: yarim yozilgan fayl hech qachon asosiy nom
# bilan ko'rinmaydi — muvaffaqiyatsizlik "zaxira bor" degan yolg'on taassurot
# bermaydi.
if ! pg_dump -Fc -f "$TMP_FILE" "$PGDATABASE" 2>&1; then
  log "XATO: pg_dump muvaffaqiyatsiz tugadi — fayl saqlanmaydi"
  rm -f "$TMP_FILE"
  exit 1
fi

SIZE=$(stat -c%s "$TMP_FILE" 2>/dev/null || echo 0)
if [ "$SIZE" -lt 1024 ]; then
  log "XATO: fayl juda kichik ($SIZE bayt) — zaxira ishonchsiz, saqlanmaydi"
  rm -f "$TMP_FILE"
  exit 1
fi

# Arxiv tuzilishini tekshirish: pg_restore --list faylni ochib TOC (mundarija)
# ni o'qiydi, bazaga hech narsa yozmaydi. Kesilgan/buzilgan fayl shu yerda
# aniq xato bilan yiqiladi — bu "zaxira olindi, lekin ochilmaydi" holatining
# oldini oladigan yagona ishonchli tekshiruv.
TOC_FILE="/tmp/.backup_toc.$$"
if ! pg_restore --list "$TMP_FILE" > "$TOC_FILE" 2>&1; then
  log "XATO: pg_restore --list faylni o'qiy olmadi — zaxira buzilgan"
  cat "$TOC_FILE" >&2
  rm -f "$TMP_FILE" "$TOC_FILE"
  exit 1
fi

ENTRIES=$(grep -c '^[0-9]' "$TOC_FILE" || true)
rm -f "$TOC_FILE"

if [ "${ENTRIES:-0}" -lt 1 ]; then
  log "XATO: zaxirada birorta ham obyekt topilmadi — bo'sh yoki noto'g'ri baza"
  rm -f "$TMP_FILE"
  exit 1
fi

mv "$TMP_FILE" "$FILE"
log "OK: $FILE ($SIZE bayt, $ENTRIES ta obyekt tekshirildi)"

# 30 kundan eski fayllarni o'chirish (standart — .env/compose orqali
# BACKUP_RETENTION_DAYS bilan o'zgartiriladi).
find "$BACKUP_DIR" -maxdepth 1 -name 'bonvi_*.dump' -mtime "+${RETENTION_DAYS}" -print 2>/dev/null | \
while IFS= read -r old; do
  rm -f "$old"
  log "eski zaxira o'chirildi ($RETENTION_DAYS kundan eski): $old"
done

log "tugadi"
