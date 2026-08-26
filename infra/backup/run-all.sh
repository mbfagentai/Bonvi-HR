#!/bin/sh
# ============================================================
#  BONVI HR — kunlik avtomatik zaxira (faqat prod `backup` konteyneri)
#
#  Bitta BACKUP_STAMP hisoblab, baza va fayl zaxiralarini ULARNING O'ZI
#  ichida ketma-ket, AVVAL BAZA keyin FAYLLAR tartibida chaqiradi —
#  sabab backup-files.sh boshidagi izohda. Ikkalasi ham ushbu konteynerda
#  ishlaydi: postgres'ga tarmoq orqali (PGHOST=postgres), fayllarga esa
#  bevosita ulangan `uploads-data:ro` volume orqali.
# ============================================================
set -eu

export BACKUP_STAMP="$(date '+%Y-%m-%d_%H%M%S')"

db_ok=1
sh /scripts/backup.sh || db_ok=0

files_ok=1
sh /scripts/backup-files.sh || files_ok=0

if [ "$db_ok" -ne 1 ] || [ "$files_ok" -ne 1 ]; then
  echo "[backup-cron $(date '+%Y-%m-%d %H:%M:%S')] XATO: zaxira TO'LIQ muvaffaqiyatli emas (baza=$db_ok, fayllar=$files_ok)" >&2
  exit 1
fi
echo "[backup-cron $(date '+%Y-%m-%d %H:%M:%S')] OK: baza va fayllar zaxirasi tayyor (stamp=$BACKUP_STAMP)"
