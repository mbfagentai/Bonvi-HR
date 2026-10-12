#!/bin/bash
# ============================================================
#  BONVI HR — zaxiraning SERVERDAN TASHQARIDAGI nusxasi (2026-10-12)
#
#  NEGA: kunlik zaxira (`backup` konteyneri) serverning O'ZIDA turadi —
#  `/opt/bonvi-hr/backups`. Server buzilsa, o'chirilsa yoki provayder
#  hisobni yopsa, baza ham, zaxira ham BIRGA yo'qoladi. Bu skript eng
#  so'nggi TO'LIQ zaxirani (baza + fayllar, bitta stamp) shu kompyuterga
#  tortib oladi va nusxani tekshiradi.
#
#  QAYERDA ISHLAYDI: egasining kompyuterida (macOS/Linux), serverda EMAS.
#  Serverga faqat O'QIYDI (ls, sha256sum, scp) — hech narsa yozmaydi va
#  o'chirmaydi.
#
#  Ishlatish:
#    bash infra/backup/pull-offsite.sh            # eng so'nggisini tortish
#    bash infra/backup/pull-offsite.sh --dry-run  # faqat nima tortilishini ko'rsatadi
#
#  Sozlash (muhit o'zgaruvchilari, hammasi ixtiyoriy):
#    BONVI_HOST       — server (sukut: root@189.74.98.57)
#    BONVI_REMOTE_DIR — serverdagi papka (sukut: /opt/bonvi-hr/backups)
#    BONVI_LOCAL_DIR  — bu kompyuterdagi papka (sukut: ~/BonviHR-zaxira)
#    BONVI_KEEP       — nechta stamp saqlanadi (sukut: 14)
#
#  ⚠️ Zaxirada xodimlarning shaxsiy ma'lumoti bor (pasport, JSHSHIR,
#  oklad). Papka faqat egasiga ochiq (700), fayllar 600. Sukut papka
#  ATAYLAB `~/Documents` da emas: macOS uni iCloud'ga sinxronlashi mumkin.
#
#  Tiklash: faylni serverga (yoki yangi serverga) `backups/` ichiga
#  qo'yib, `./bonvi restore <fayl>` — `infra/backup/restore.sh`.
# ============================================================
set -euo pipefail

HOST="${BONVI_HOST:-root@189.74.98.57}"
REMOTE_DIR="${BONVI_REMOTE_DIR:-/opt/bonvi-hr/backups}"
LOCAL_DIR="${BONVI_LOCAL_DIR:-$HOME/BonviHR-zaxira}"
KEEP="${BONVI_KEEP:-14}"
DRY_RUN=0
[ "${1:-}" = "--dry-run" ] && DRY_RUN=1

log() { echo "[zaxira-nusxa $(date '+%Y-%m-%d %H:%M:%S')] $*"; }
fail() { log "XATO: $*" >&2; exit 1; }

case "$KEEP" in ''|*[!0-9]*) fail "BONVI_KEEP butun son bo'lishi kerak: $KEEP" ;; esac
[ "$KEEP" -ge 1 ] || fail "BONVI_KEEP kamida 1"

SSH=(ssh -o BatchMode=yes -o ConnectTimeout=15 "$HOST")

# ── 1. Serverdagi eng so'nggi TO'LIQ stamp: baza ham, fayllar ham bor ──
# `.part` — yozilayotgan fayl, hisobga olinmaydi (backup.sh tugagach nomini o'zgartiradi).
LISTING="$("${SSH[@]}" "cd '$REMOTE_DIR' && ls -1 bonvi_*.dump bonvi_uploads_*.tar.gz 2>/dev/null" || true)"
[ -n "$LISTING" ] || fail "serverda zaxira topilmadi ($HOST:$REMOTE_DIR)"

STAMP=""
DB_FILE=""
FILES_FILE=""
# Stamp: `YYYY-MM-DD_HHMMSS` — nom oxirida; leksik tartib = vaqt tartibi.
for stamp in $(printf '%s\n' "$LISTING" | sed -nE 's/.*_([0-9]{4}-[0-9]{2}-[0-9]{2}_[0-9]{6})\.(dump|tar\.gz)$/\1/p' | sort -u -r); do
  db="$(printf '%s\n' "$LISTING" | grep -E "^bonvi_.+_${stamp}\.dump$" | grep -v '^bonvi_uploads_' | head -1 || true)"
  files="bonvi_uploads_${stamp}.tar.gz"
  if [ -n "$db" ] && printf '%s\n' "$LISTING" | grep -qx "$files"; then
    STAMP="$stamp"; DB_FILE="$db"; FILES_FILE="$files"
    break
  fi
done
[ -n "$STAMP" ] || fail "baza va fayllar zaxirasi bir xil stamp bilan topilmadi"
log "eng so'nggi to'liq zaxira: $STAMP ($DB_FILE, $FILES_FILE)"

# ── 2. Allaqachon tortilganmi ─────────────────────────────────
TARGET="$LOCAL_DIR/$STAMP"
if [ -f "$TARGET/.ok" ]; then
  log "bu stamp allaqachon shu yerda va tekshirilgan: $TARGET — yangisi yo'q"
  exit 0
fi

if [ "$DRY_RUN" -eq 1 ]; then
  SIZES="$("${SSH[@]}" "cd '$REMOTE_DIR' && du -h '$DB_FILE' '$FILES_FILE'")"
  log "DRY-RUN: tortiladi → $TARGET"
  printf '%s\n' "$SIZES"
  exit 0
fi

# ── 3. Tortish va tekshirish ──────────────────────────────────
umask 077
mkdir -p "$LOCAL_DIR"
chmod 700 "$LOCAL_DIR"
TMP="$LOCAL_DIR/.${STAMP}.part"
rm -rf "$TMP"
mkdir -p "$TMP"

REMOTE_SUMS="$("${SSH[@]}" "cd '$REMOTE_DIR' && sha256sum '$DB_FILE' '$FILES_FILE'")"

for f in "$DB_FILE" "$FILES_FILE"; do
  log "tortilmoqda: $f"
  scp -q -o BatchMode=yes -o ConnectTimeout=15 "$HOST:$REMOTE_DIR/$f" "$TMP/$f"
done

# sha256: macOS'da `shasum -a 256`, Linux'da `sha256sum` — chiqish shakli bir xil.
if command -v sha256sum >/dev/null 2>&1; then SUM=(sha256sum); else SUM=(shasum -a 256); fi
LOCAL_SUMS="$(cd "$TMP" && "${SUM[@]}" "$DB_FILE" "$FILES_FILE")"

norm() { awk '{print $1, $2}' | sed 's/ \*/ /' | sort; }
if [ "$(printf '%s\n' "$REMOTE_SUMS" | norm)" != "$(printf '%s\n' "$LOCAL_SUMS" | norm)" ]; then
  rm -rf "$TMP"
  fail "nazorat summasi mos kelmadi — nusxa saqlanmadi (tarmoq uzilgan bo'lishi mumkin, qayta urinib ko'ring)"
fi

chmod 600 "$TMP"/*
printf '%s\n' "$LOCAL_SUMS" > "$TMP/SHA256SUMS"
touch "$TMP/.ok"
mv "$TMP" "$TARGET"
log "OK: $TARGET ($(du -sh "$TARGET" | awk '{print $1}'))"

# ── 4. Eskilarini tozalash: oxirgi KEEP ta stamp qoladi ────────
# Faqat tekshirilgan (`.ok` bor) stamp papkalari sanaladi; boshqa narsaga tegilmaydi.
OLD="$(find "$LOCAL_DIR" -mindepth 1 -maxdepth 1 -type d -name '20??-??-??_??????' -exec test -f '{}/.ok' ';' -print | sort -r | tail -n +"$((KEEP + 1))")"
if [ -n "$OLD" ]; then
  printf '%s\n' "$OLD" | while read -r dir; do
    log "eski nusxa o'chirildi: $dir"
    rm -rf "$dir"
  done
fi
