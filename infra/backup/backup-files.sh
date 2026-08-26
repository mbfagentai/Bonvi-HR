#!/bin/sh
# ============================================================
#  BONVI HR — fayl (rasm, hujjat) zaxirasi
#
#  `uploads-data` volume'ini (Storage__RootPath, IFileStorage yozadigan
#  joy) tar.gz qilib arxivlaydi. backup.sh bilan BIR XIL BACKUP_STAMP
#  ishlatiladi — shu sababli DB dump va fayl arxivi nomlarida bir xil
#  sana-vaqt turadi va ular BIR ZAXIRA HODISASI sifatida bog'lanadi
#  (bazadagi `photo_key` shu arxivdagi faylga ishora qiladi).
#
#  Ishga tushirish tartibi muhim: bu skript HAR DOIM backup.sh dan KEYIN
#  chaqiriladi (./bonvi backup va prod cron ikkalasida ham). Sabab:
#  ilova avval faylni yozadi, keyin bazada unga ishora qiladi — ya'ni
#  fayl har doim o'zini bildiruvchi DB yozuvidan OLDIN paydo bo'ladi.
#  Shu tartibda: agar backup oralig'ida yangi xodim/rasm qo'shilsa, eski
#  (avval olingan) DB dump'da unga ishora YO'Q — demak keyin olingan fayl
#  arxivida "ortiqcha" fayl bo'ladi, bu ZARARSIZ. Aksincha tartibda esa
#  DB dump fayl arxividan OLDIN yo'q bo'lgan faylga ishora qilib qolishi
#  mumkin edi — bu tiklashda "rasm topilmadi" degan buzilgan holat.
# ============================================================
set -eu

SRC_DIR="${UPLOADS_SRC_DIR:-/uploads}"
BACKUP_DIR="${BACKUP_DIR:-/backups}"
RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-30}"
STAMP="${BACKUP_STAMP:?BACKUP_STAMP kerak (backup.sh bilan bir xil qiymat)}"

log() { echo "[backup-files $(date '+%Y-%m-%d %H:%M:%S')] $*"; }

mkdir -p "$BACKUP_DIR"

# Papka umuman yo'q yoki ichida hech narsa yo'q — bu xato emas (masalan
# hali birorta ham xodimga rasm yuklanmagan yangi muhit), shu sababli
# "zaxira yo'q" deb jimgina o'tkazib yuboramiz, lekin buni ANIQ yozamiz —
# keyinchalik "nega fayl zaxirasi yo'q edi" degan savolga javob bo'lsin.
if [ ! -d "$SRC_DIR" ] || [ -z "$(find "$SRC_DIR" -type f -print -quit 2>/dev/null)" ]; then
  log "OGOHLANTIRISH: $SRC_DIR bo'sh yoki topilmadi — fayl zaxirasi o'tkazib yuborildi (bu xato emas, lekin diqqat talab qiladi)"
  exit 0
fi

FILE="$BACKUP_DIR/bonvi_uploads_${STAMP}.tar.gz"
TMP_FILE="${FILE}.part"

log "boshlandi: $SRC_DIR → $FILE"

# .part kengaytmasi — backup.sh dagi bilan bir xil naqsh: yarim yozilgan
# arxiv hech qachon asosiy nom bilan ko'rinmaydi.
if ! tar -czf "$TMP_FILE" -C "$SRC_DIR" . 2>&1; then
  log "XATO: tar muvaffaqiyatsiz tugadi — fayl saqlanmaydi"
  rm -f "$TMP_FILE"
  exit 1
fi

SIZE=$(stat -c%s "$TMP_FILE" 2>/dev/null || echo 0)
if [ "$SIZE" -lt 100 ]; then
  log "XATO: arxiv juda kichik ($SIZE bayt) — ishonchsiz, saqlanmaydi"
  rm -f "$TMP_FILE"
  exit 1
fi

# tar -tzf: butun siqilgan oqimni oxirigacha o'qiydi (gzip CRC tekshiruvi
# shu bilan birga bajariladi) va mundarijani chiqaradi — diskka hech
# narsa yozmaydi. Kesilgan/buzilgan arxiv shu yerda aniq xato bilan
# yiqiladi, shu sababli "bo'sh yoki buzilgan arxiv = zaxira bor" degan
# yolg'on holat bo'lmaydi.
TOC_FILE="/tmp/.files_toc.$$"
if ! tar -tzf "$TMP_FILE" > "$TOC_FILE" 2>&1; then
  log "XATO: arxiv buzilgan — tar -tzf o'qiy olmadi"
  cat "$TOC_FILE" >&2
  rm -f "$TMP_FILE" "$TOC_FILE"
  exit 1
fi

ENTRIES=$(grep -c . "$TOC_FILE" || true)
rm -f "$TOC_FILE"

if [ "${ENTRIES:-0}" -lt 1 ]; then
  log "XATO: arxivda birorta ham yozuv yo'q"
  rm -f "$TMP_FILE"
  exit 1
fi

mv "$TMP_FILE" "$FILE"
log "OK: $FILE ($SIZE bayt, $ENTRIES ta yozuv tekshirildi)"

find "$BACKUP_DIR" -maxdepth 1 -name 'bonvi_uploads_*.tar.gz' -mtime "+${RETENTION_DAYS}" -print 2>/dev/null | \
while IFS= read -r old; do
  rm -f "$old"
  log "eski fayl zaxirasi o'chirildi (${RETENTION_DAYS} kundan eski): $old"
done

log "tugadi"
