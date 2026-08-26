#!/bin/sh
# ============================================================
#  BONVI HR — zaxira konteynerining kirish nuqtasi (faqat production)
#
#  `postgres:18.6-alpine` image'i Alpine asosida — busybox `crond` allaqachon
#  bor, alohida paket o'rnatish shart emas. Shu sababli kunlik jadval uchun
#  Hangfire (qo'shimcha .NET jarayoni, dashboard, PostgreSQL storage jadvali)
#  yoki xost cron (loyihaning "xostga hech narsa o'rnatilmaydi" qoidasiga
#  zid) o'rniga shu — compose ichidagi alohida, yengil konteyner — tanlandi.
#  Sabab batafsil: PLAN.md S6 (Hangfire nima uchun ishlatilmagani) va shu
#  konteynerning docker-compose.prod.yml dagi izohi.
#
#  DIQQAT: konteyner soati standart UTC. BACKUP_CRON_SCHEDULE shunga qarab
#  belgilanadi (standart: 22:00 UTC = 03:00 Toshkent, UTC+5, yozgi vaqt yo'q).
# ============================================================
set -eu

SCHEDULE="${BACKUP_CRON_SCHEDULE:-0 22 * * *}"

mkdir -p /etc/crontabs
# stdout/stderr ni PID 1 ga yo'naltiramiz — natija `docker logs backup`
# bilan ko'rinadi, alohida log fayl kerak emas.
echo "$SCHEDULE sh /scripts/run-all.sh >> /proc/1/fd/1 2>> /proc/1/fd/2" > /etc/crontabs/root

echo "[backup-cron] jadval: '$SCHEDULE' (konteyner soati, UTC)"
echo "[backup-cron] papka:  ${BACKUP_DIR:-/backups}"
echo "[backup-cron] saqlash: ${BACKUP_RETENTION_DAYS:-30} kun"

exec crond -f -l 2
