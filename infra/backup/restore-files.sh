#!/bin/sh
# ============================================================
#  BONVI HR — fayl (rasm, hujjat) zaxirasidan tiklash
#
#  `./bonvi restore <db-fayl>` bazani tiklagandan KEYIN, agar bir xil
#  BACKUP_STAMP'li tar.gz topilsa, shu skriptni avtomatik chaqiradi —
#  qo'lda alohida buyruq sifatida ochiq qilinmagan, chunki bazasiz
#  fayllarni (yoki aksincha) alohida tiklash `photo_key` va haqiqiy
#  fayl orasidagi mosligini buzish xavfini oshiradi.
#
#  Ishlatish (konteyner ichida): restore-files.sh /backups/nomi.tar.gz
# ============================================================
set -eu

FILE="${1:?fayl nomi kerak: restore-files.sh /backups/nomi.tar.gz}"
DEST_DIR="${UPLOADS_DEST_DIR:-/uploads}"
[ -f "$FILE" ] || { echo "[restore-files] XATO: fayl topilmadi: $FILE" >&2; exit 1; }
[ -n "$DEST_DIR" ] || { echo "[restore-files] XATO: DEST_DIR bo'sh" >&2; exit 1; }

log() { echo "[restore-files $(date '+%Y-%m-%d %H:%M:%S')] $*"; }

log "tekshirilmoqda: $FILE"
if ! tar -tzf "$FILE" >/dev/null 2>&1; then
  log "XATO: arxiv buzilgan yoki tar.gz emas — tiklanmadi"
  exit 1
fi

mkdir -p "$DEST_DIR"

# Bazadagi --clean ga mos ravishda: tiklashdan oldin HOZIRGI fayllarni
# tozalaymiz — aks holda eski (endi bazada ishora qilinmaydigan) fayllar
# yangilari bilan aralashib qoladi. `${DEST_DIR:?}` — DEST_DIR bo'sh
# bo'lib qolsa (masalan o'zgaruvchi eksport qilinmasa) `rm -rf /*`
# ishlab ketmasligi uchun himoya.
log "eski fayllar tozalanmoqda: $DEST_DIR"
find "${DEST_DIR:?}" -mindepth 1 -delete 2>/dev/null || true

log "tiklanmoqda: $DEST_DIR ← $FILE"
tar -xzf "$FILE" -C "$DEST_DIR"
log "tugadi"
